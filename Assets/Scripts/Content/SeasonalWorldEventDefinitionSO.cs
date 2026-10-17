using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SeasonalWildlifeArrivalQualification
{
    None = 0,
    Predatory = 1,
    NonHostile = 2
}

[System.Serializable]
public sealed class SeasonalWildlifeArrivalProfile
{
    public string speciesId = string.Empty;
    [Min(1)] public int exactCount;
    public string requiredHabitatId = string.Empty;
    public SeasonalWildlifeArrivalQualification qualification;

    public bool IsConfigured =>
        !string.IsNullOrEmpty(speciesId)
        || exactCount != 0
        || !string.IsNullOrEmpty(requiredHabitatId)
        || qualification != SeasonalWildlifeArrivalQualification.None;

    public IReadOnlyList<string> Validate(string ownerId)
    {
        List<string> errors = new();
        string owner = string.IsNullOrWhiteSpace(ownerId)
            ? "seasonal event"
            : ownerId;
        if (!IsConfigured)
        {
            return errors;
        }
        if (!Canonical(speciesId))
            errors.Add($"'{owner}' seasonal wildlife species id must be canonical.");
        if (exactCount is < 1 or > 8)
            errors.Add($"'{owner}' seasonal wildlife count must be within [1, 8].");
        if (!Canonical(requiredHabitatId))
            errors.Add($"'{owner}' seasonal wildlife habitat id must be canonical.");
        if (qualification == SeasonalWildlifeArrivalQualification.None
            || !System.Enum.IsDefined(
                typeof(SeasonalWildlifeArrivalQualification),
                qualification))
            errors.Add($"'{owner}' seasonal wildlife qualification is invalid.");
        return errors;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(
            value,
            value.Trim(),
            System.StringComparison.Ordinal);
}

[System.Serializable]
public sealed class SeasonalDriftCargoProfile
{
    public string itemId = string.Empty;
    [Min(1)] public int exactQuantity;

    public bool IsConfigured =>
        !string.IsNullOrEmpty(itemId) || exactQuantity != 0;

    public IReadOnlyList<string> Validate(string ownerId)
    {
        List<string> errors = new();
        string owner = string.IsNullOrWhiteSpace(ownerId)
            ? "seasonal event"
            : ownerId;
        if (!IsConfigured)
            return errors;
        if (string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(
                itemId,
                itemId.Trim(),
                System.StringComparison.Ordinal))
            errors.Add($"'{owner}' seasonal drift-cargo item id must be canonical.");
        if (exactQuantity is < 1 or > 20)
            errors.Add($"'{owner}' seasonal drift-cargo quantity must be within [1, 20].");
        return errors;
    }
}

[System.Serializable]
public sealed class SeasonalManaLightningProfile
{
    public List<int> eligibleBuildingDefinitionIds = new();
    [Min(0f)] public float minimumHeat;
    [Min(0f)] public float minimumFault;
    [Min(1f)] public float riskWindowSeconds = 30f;
    [Range(0f, 1f)] public float additionalIgnitionChancePerWindow;
    [Range(0f, 1f)] public float ignitionIntensity;

    public bool IsConfigured =>
        (eligibleBuildingDefinitionIds?.Count ?? 0) > 0
        || minimumHeat != 0f
        || minimumFault != 0f
        || additionalIgnitionChancePerWindow != 0f
        || ignitionIntensity != 0f;

    public IReadOnlyList<string> Validate(string ownerId)
    {
        List<string> errors = new();
        string owner = string.IsNullOrWhiteSpace(ownerId)
            ? "seasonal event"
            : ownerId;
        if (!IsConfigured)
            return errors;
        int[] eligible = (eligibleBuildingDefinitionIds ?? new()).ToArray();
        if (eligible.Length == 0
            || eligible.Any(value => value <= 0)
            || eligible.Distinct().Count() != eligible.Length)
            errors.Add($"'{owner}' mana-lightning facility IDs must be positive and unique.");
        if (!FiniteNonNegative(minimumHeat) || !FiniteNonNegative(minimumFault))
            errors.Add($"'{owner}' mana-lightning Heat/Fault thresholds must be finite and nonnegative.");
        if (!FinitePositive(riskWindowSeconds))
            errors.Add($"'{owner}' mana-lightning risk window must be finite and positive.");
        if (!FinitePositive(additionalIgnitionChancePerWindow)
            || additionalIgnitionChancePerWindow > 1f)
            errors.Add($"'{owner}' mana-lightning risk contribution must be within (0, 1].");
        if (!FinitePositive(ignitionIntensity) || ignitionIntensity > 1f)
            errors.Add($"'{owner}' mana-lightning intensity must be within (0, 1].");
        return errors;
    }

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

public enum SeasonalSpecialExpeditionPurpose
{
    None = 0,
    ManaCrystalYield = 1,
    TruthGuardianEcho = 2
}

[System.Serializable]
public sealed class SeasonalExpeditionPhysicalRewardProfile
{
    public string itemId = string.Empty;
    public string displayLabel = string.Empty;
    [Min(1)] public int exactQuantity;
}

[System.Serializable]
public sealed class SeasonalSpecialExpeditionProfile
{
    public SeasonalSpecialExpeditionPurpose purpose;
    public string siteArchetypeId = string.Empty;
    public string title = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int minimumDistanceSteps = 1;
    [Min(1)] public int maximumDistanceSteps = 1;
    [Min(0f)] public float recommendedDanger;
    [Min(1f)] public float durationSeconds = 90f;
    [Min(1)] public int requiredMembers = 1;
    [Min(0f)] public float recommendedPower;
    [Range(1, 6)] public int campaignOrder = 1;
    public string authoredEncounterId = string.Empty;
    [TextArea] public string encounterPreviewText = string.Empty;
    [TextArea] public string encounterRewardPreviewText = string.Empty;
    public string factionId = string.Empty;
    public List<SeasonalExpeditionPhysicalRewardProfile> physicalRewards = new();

    public bool IsConfigured => purpose != SeasonalSpecialExpeditionPurpose.None;

    public IReadOnlyList<string> Validate(string ownerId)
    {
        List<string> errors = new();
        string owner = string.IsNullOrWhiteSpace(ownerId)
            ? "seasonal event"
            : ownerId;
        if (!IsConfigured)
            return errors;
        if (!System.Enum.IsDefined(typeof(SeasonalSpecialExpeditionPurpose), purpose))
            errors.Add($"'{owner}' special-expedition purpose is invalid.");
        foreach (string value in new[]
                 {
                     siteArchetypeId, title, description, authoredEncounterId,
                     encounterPreviewText, encounterRewardPreviewText, factionId
                 })
        {
            if (!Canonical(value))
                errors.Add($"'{owner}' special-expedition text and IDs must be canonical.");
        }
        if (minimumDistanceSteps < 1
            || maximumDistanceSteps < minimumDistanceSteps)
            errors.Add($"'{owner}' special-expedition distance range is invalid.");
        if (!FiniteNonNegative(recommendedDanger)
            || !FinitePositive(durationSeconds)
            || requiredMembers is < 1 or > 5
            || !FiniteNonNegative(recommendedPower)
            || campaignOrder is < 1 or > 6)
            errors.Add($"'{owner}' special-expedition risk/cost values are invalid.");
        SeasonalExpeditionPhysicalRewardProfile[] rewards =
            (physicalRewards ?? new()).ToArray();
        if (rewards.Length == 0
            || rewards.Any(value => value == null
                || !Canonical(value.itemId)
                || !Canonical(value.displayLabel)
                || value.exactQuantity <= 0)
            || rewards.Select(value => value.itemId).Distinct(
                System.StringComparer.Ordinal).Count() != rewards.Length)
            errors.Add($"'{owner}' special-expedition physical rewards must be canonical, positive, and unique.");
        return errors;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), System.StringComparison.Ordinal);
    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

[System.Serializable]
public sealed class SeasonalFeedSelfHeatingFireProfile
{
    [Min(1)] public int minimumPhysicalQuantity;
    [Range(0.01f, 1f)] public float ignitionIntensity;

    public bool IsConfigured =>
        minimumPhysicalQuantity != 0 || ignitionIntensity != 0f;

    public IReadOnlyList<string> Validate(string ownerId)
    {
        List<string> errors = new();
        if (!IsConfigured)
        {
            return errors;
        }
        if (minimumPhysicalQuantity < 1)
        {
            errors.Add(
                $"'{ownerId}' feed self-heating requires positive physical stock.");
        }
        if (float.IsNaN(ignitionIntensity)
            || float.IsInfinity(ignitionIntensity)
            || ignitionIntensity < 0.01f
            || ignitionIntensity > 1f)
        {
            errors.Add(
                $"'{ownerId}' feed self-heating ignition intensity must be within [0.01, 1].");
        }
        return errors;
    }
}

[CreateAssetMenu(fileName = "SeasonalWorldEvent", menuName = "DungeonStory/V20/Seasonal World Event")]
public sealed class SeasonalWorldEventDefinitionSO : V20AuthoredContentSO
{
    public Season season;
    [Min(1)] public int minimumDurationDays = 1;
    [Min(1)] public int maximumDurationDays = 1;
    public float worldWaterRegenerationMultiplier = 1f;
    [Range(0.01f, 1f)] public float powerAvailableSupplyCapacityMultiplier = 1f;
    [Range(0.01f, 1f)] public float pipedWaterThroughputMultiplier = 1f;
    public float pipedWaterFreezeThresholdC;
    public float pipedWaterRecoveryThresholdC = 2f;
    [Range(0, 10)] public int cropPrimaryBatchLossPercent;
    public string requiredWeatherFrontId = string.Empty;
    public SeasonalWildlifeArrivalProfile wildlifeArrivalProfile = new();
    public SeasonalDriftCargoProfile driftCargoProfile = new();
    public SeasonalManaLightningProfile manaLightningProfile = new();
    public SeasonalSpecialExpeditionProfile specialExpeditionProfile = new();
    public SeasonalFeedSelfHeatingFireProfile feedSelfHeatingFireProfile = new();
    public List<string> affectedDomainIds = new();
    public V20ContentRequirementSet triggerRequirements = new();
    public List<V20ContentEffect> startEffects = new();
    public List<V20ContentEffect> dailyEffects = new();
    public List<V20ContentEffect> endEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (minimumDurationDays < 1 || maximumDurationDays < minimumDurationDays)
            errors.Add($"'{StableId}' duration range is invalid.");
        if (float.IsNaN(worldWaterRegenerationMultiplier)
            || float.IsInfinity(worldWaterRegenerationMultiplier)
            || worldWaterRegenerationMultiplier <= 0f
            || worldWaterRegenerationMultiplier > 1f)
            errors.Add($"'{StableId}' world-water regeneration multiplier must be finite and within (0, 1].");
        if (!IsFiniteMultiplier(powerAvailableSupplyCapacityMultiplier))
            errors.Add($"'{StableId}' power available-supply multiplier must be finite and within (0, 1].");
        if (!IsFiniteMultiplier(pipedWaterThroughputMultiplier))
            errors.Add($"'{StableId}' piped-water throughput multiplier must be finite and within (0, 1].");
        if (float.IsNaN(pipedWaterFreezeThresholdC)
            || float.IsInfinity(pipedWaterFreezeThresholdC)
            || float.IsNaN(pipedWaterRecoveryThresholdC)
            || float.IsInfinity(pipedWaterRecoveryThresholdC)
            || pipedWaterRecoveryThresholdC <= pipedWaterFreezeThresholdC)
            errors.Add($"'{StableId}' piped-water recovery threshold must be finite and above its freeze threshold.");
        if (cropPrimaryBatchLossPercent is < 0 or > 10)
            errors.Add($"'{StableId}' crop primary-batch loss percent must be within [0, 10].");
        if (requiredWeatherFrontId == null
            || requiredWeatherFrontId.Length > 0
            && (string.IsNullOrWhiteSpace(requiredWeatherFrontId)
                || !string.Equals(
                    requiredWeatherFrontId,
                    requiredWeatherFrontId.Trim(),
                    System.StringComparison.Ordinal)))
            errors.Add($"'{StableId}' required weather-front id must be an optional canonical ID.");
        errors.AddRange((wildlifeArrivalProfile ?? new())
            .Validate(StableId));
        errors.AddRange((driftCargoProfile ?? new())
            .Validate(StableId));
        errors.AddRange((manaLightningProfile ?? new()).Validate(StableId));
        errors.AddRange((specialExpeditionProfile ?? new()).Validate(StableId));
        errors.AddRange((feedSelfHeatingFireProfile ?? new()).Validate(StableId));
        bool hasWildlifeArrival =
            wildlifeArrivalProfile?.IsConfigured == true;
        bool hasDriftCargo = driftCargoProfile?.IsConfigured == true;
        bool hasManaLightning = manaLightningProfile?.IsConfigured == true;
        bool hasSpecialExpedition = specialExpeditionProfile?.IsConfigured == true;
        bool hasFeedSelfHeating =
            feedSelfHeatingFireProfile?.IsConfigured == true;
        if (hasWildlifeArrival && hasDriftCargo)
            errors.Add($"'{StableId}' cannot combine wildlife arrival and drift cargo.");
        if (hasWildlifeArrival
            && !(affectedDomainIds ?? new()).Contains(
                "wildlife",
                System.StringComparer.Ordinal))
            errors.Add($"'{StableId}' seasonal wildlife arrival requires the wildlife domain.");
        if (hasWildlifeArrival
            && (startEffects ?? new()).Any(value =>
                value != null && value.IsValid))
            errors.Add($"'{StableId}' seasonal wildlife arrival must not also author a start effect.");
        if (hasDriftCargo
            && !(affectedDomainIds ?? new()).Contains(
                "logistics",
                System.StringComparer.Ordinal))
            errors.Add($"'{StableId}' seasonal drift cargo requires the logistics domain.");
        if (hasDriftCargo
            && (startEffects ?? new()).Any(value =>
                value != null && value.IsValid))
            errors.Add($"'{StableId}' seasonal drift cargo must not also author a start effect or reward.");
        if (hasManaLightning
            && !(affectedDomainIds ?? new()).Contains(
                "facility",
                System.StringComparer.Ordinal))
            errors.Add($"'{StableId}' mana lightning requires the facility domain.");
        if (hasSpecialExpedition
            && !(affectedDomainIds ?? new()).Contains(
                "expedition",
                System.StringComparer.Ordinal))
            errors.Add($"'{StableId}' special expedition requires the expedition domain.");
        if (hasFeedSelfHeating
            && (!(affectedDomainIds ?? new()).Contains(
                    "husbandry",
                    System.StringComparer.Ordinal)
                || !(affectedDomainIds ?? new()).Contains(
                    "facility",
                    System.StringComparer.Ordinal)))
        {
            errors.Add(
                $"'{StableId}' feed self-heating requires husbandry and facility domains.");
        }
        if ((hasManaLightning || hasSpecialExpedition)
            && (startEffects ?? new()).Concat(dailyEffects ?? new())
                .Concat(endEffects ?? new())
                .Any(value => value != null && value.IsValid))
            errors.Add($"'{StableId}' seasonal arcane profiles must not also author abstract effects or rewards.");
        if (cropPrimaryBatchLossPercent > 0
            && !(affectedDomainIds ?? new()).Contains(
                "agriculture",
                System.StringComparer.Ordinal))
            errors.Add($"'{StableId}' crop primary-batch loss requires the agriculture domain.");
        if ((powerAvailableSupplyCapacityMultiplier < 1f
                || pipedWaterThroughputMultiplier < 1f)
            && !(affectedDomainIds ?? new()).Contains(
                "facility",
                System.StringComparer.Ordinal))
            errors.Add($"'{StableId}' infrastructure capacity contribution requires the facility domain.");
        if ((affectedDomainIds ?? new()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Count() < 2)
            errors.Add($"'{StableId}' must affect at least two domains.");
        if (cropPrimaryBatchLossPercent == 0
            && string.IsNullOrEmpty(requiredWeatherFrontId)
            && powerAvailableSupplyCapacityMultiplier == 1f
            && pipedWaterThroughputMultiplier == 1f
            && !hasWildlifeArrival
            && !hasDriftCargo
            && !hasManaLightning
            && !hasSpecialExpedition
            && !hasFeedSelfHeating
            && !(startEffects ?? new()).Concat(dailyEffects ?? new()).Concat(endEffects ?? new()).Any(value => value != null && value.IsValid))
            errors.Add($"'{StableId}' requires a mechanical effect.");
        return errors;
    }

    private static bool IsFiniteMultiplier(float value) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value > 0f
        && value <= 1f;
}
