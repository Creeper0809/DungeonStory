using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public sealed class CropCycleInputRequirementSnapshot
{
    internal CropCycleInputRequirementSnapshot(
        string cropId,
        bool indoor,
        SurvivalWeatherType weather,
        float milestoneConsumptionMultiplier,
        string selectedFuelItemId,
        IReadOnlyDictionary<string, int> requirements,
        string sourceDigest)
    {
        selectedFuelItemId ??= string.Empty;
        RequireCanonical(cropId, nameof(cropId));
        if (!Enum.IsDefined(typeof(SurvivalWeatherType), weather)
            || !float.IsFinite(milestoneConsumptionMultiplier)
            || milestoneConsumptionMultiplier is < 0.1f or > 1f
            || requirements == null
            || requirements.Count == 0
            || requirements.Any(value => !Canonical(value.Key)
                || value.Value <= 0)
            || (selectedFuelItemId.Length > 0
                && !Canonical(selectedFuelItemId))
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Crop cycle input-requirement snapshot is invalid.");
        }
        CropId = cropId;
        Indoor = indoor;
        Weather = weather;
        MilestoneConsumptionMultiplier = milestoneConsumptionMultiplier;
        SelectedFuelItemId = selectedFuelItemId;
        SortedDictionary<string, int> canonicalRequirements =
            new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> requirement in requirements)
            canonicalRequirements.Add(requirement.Key, requirement.Value);
        Requirements = new ReadOnlyDictionary<string, int>(
            canonicalRequirements);
        SourceDigest = sourceDigest;
    }

    public string CropId { get; }
    public bool Indoor { get; }
    public SurvivalWeatherType Weather { get; }
    public float MilestoneConsumptionMultiplier { get; }
    public string SelectedFuelItemId { get; }
    public IReadOnlyDictionary<string, int> Requirements { get; }
    public string SourceDigest { get; }

    private static void RequireCanonical(string value, string name)
    {
        if (!Canonical(value))
            throw new ArgumentException(
                "A canonical crop input identifier is required.",
                name);
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface ICropCycleInputRequirementQuery
{
    CropCycleInputRequirementSnapshot Capture(
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability,
        string excludedDestinationId,
        SurvivalWeatherType weather,
        float milestoneConsumptionMultiplier,
        Func<string, string, int> countAvailableStock);

    CropCycleInputRequirementSnapshot RehydrateAndValidate(
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability,
        string excludedDestinationId,
        SurvivalWeatherType weather,
        float milestoneConsumptionMultiplier,
        string selectedFuelItemId,
        IReadOnlyDictionary<string, int> requirements,
        string sourceDigest);
}

/// <summary>
/// Shared, side-effect-free authority for the exact physical inputs selected
/// by a crop cycle. Runtime planning and audit descriptors call this same
/// implementation; availability only chooses between authored fuel candidates.
/// </summary>
public sealed class CropCycleInputRequirementAuthority :
    ICropCycleInputRequirementQuery
{
    public const string Schema = "crop-cycle-input-requirements@1";
    public const string CompostItemId = "material:compost";
    public const string CleanWaterItemId = "resource:clean-water";

    private readonly IResourceEconomyContentCatalog catalog;

    public CropCycleInputRequirementAuthority(
        IResourceEconomyContentCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public CropCycleInputRequirementSnapshot Capture(
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability,
        string excludedDestinationId,
        SurvivalWeatherType weather,
        float milestoneConsumptionMultiplier,
        Func<string, string, int> countAvailableStock)
    {
        if (crop == null) throw new ArgumentNullException(nameof(crop));
        if (ability == null) throw new ArgumentNullException(nameof(ability));
        if (!Enum.IsDefined(typeof(SurvivalWeatherType), weather))
            throw new ArgumentOutOfRangeException(nameof(weather));
        if (!float.IsFinite(milestoneConsumptionMultiplier))
            throw new ArgumentOutOfRangeException(
                nameof(milestoneConsumptionMultiplier));
        string excluded = excludedDestinationId ?? string.Empty;
        if (excluded.Length > 0
            && (!string.Equals(excluded, excluded.Trim(),
                    StringComparison.Ordinal)
                || excluded.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException(
                "Crop input destination must be canonical.",
                nameof(excludedDestinationId));
        }

        float consumptionMultiplier = Mathf.Clamp(
            milestoneConsumptionMultiplier,
            0.1f,
            1f);
        Dictionary<string, int> requirements = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(crop.SeedItemId)
            || !catalog.TryGetItem(crop.SeedItemId, out _))
        {
            throw new InvalidOperationException(
                "Crop requires a missing authored physical seed-lot item: "
                + crop.CropId);
        }
        requirements[crop.SeedItemId] = 1;

        float waterRate = ability.WaterMultiplier;
        if (!ability.Indoor
            && weather is SurvivalWeatherType.Rain
                or SurvivalWeatherType.Storm)
        {
            waterRate *= 0.5f;
        }
        int water = crop.DailyWater <= 0f
            ? 0
            : Mathf.Max(
                1,
                Mathf.CeilToInt(
                    crop.DailyWater
                    * (crop.GrowthHours / 24f)
                    * waterRate
                    * consumptionMultiplier));
        if (water > 0)
        {
            RequireItem(CleanWaterItemId, crop.CropId);
            requirements[CleanWaterItemId] = water;
        }

        if (ability.CompostPerCycle > 0)
        {
            RequireItem(CompostItemId, crop.CropId);
            requirements[CompostItemId] = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    ability.CompostPerCycle * consumptionMultiplier));
        }

        string selectedFuelItemId = string.Empty;
        if (ability.FuelPerCycle > 0)
        {
            selectedFuelItemId = ResolveFuel(
                excluded,
                countAvailableStock);
            requirements[selectedFuelItemId] = ability.FuelPerCycle;
        }

        foreach (ItemAmountDefinition supply in ability.CycleSupplyInputs)
        {
            if (supply == null)
                throw new InvalidOperationException(
                    "Crop cycle supply cannot be null: " + crop.CropId);
            RequireItem(supply.ItemId, crop.CropId);
            requirements.TryGetValue(supply.ItemId, out int current);
            requirements[supply.ItemId] = checked(current + supply.Amount);
        }

        string sourceDigest = CaptureSourceDigest(
            crop,
            ability,
            excluded,
            weather,
            consumptionMultiplier,
            selectedFuelItemId,
            requirements);
        return new CropCycleInputRequirementSnapshot(
            crop.CropId,
            ability.Indoor,
            weather,
            consumptionMultiplier,
            selectedFuelItemId,
            requirements,
            sourceDigest);
    }

    public CropCycleInputRequirementSnapshot RehydrateAndValidate(
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability,
        string excludedDestinationId,
        SurvivalWeatherType weather,
        float milestoneConsumptionMultiplier,
        string selectedFuelItemId,
        IReadOnlyDictionary<string, int> requirements,
        string sourceDigest)
    {
        selectedFuelItemId ??= string.Empty;
        if (requirements == null
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                sourceDigest))
        {
            throw new InvalidOperationException(
                "Frozen crop input authority is incomplete.");
        }
        CropCycleInputRequirementSnapshot expected = Capture(
            crop,
            ability,
            excludedDestinationId,
            weather,
            milestoneConsumptionMultiplier,
            (itemId, _) => string.Equals(
                itemId,
                selectedFuelItemId,
                StringComparison.Ordinal)
                    ? 1
                    : 0);
        bool vectorExact = expected.Requirements.Count == requirements.Count
            && expected.Requirements.All(value =>
                requirements.TryGetValue(value.Key, out int quantity)
                && quantity == value.Value);
        if (!string.Equals(
                expected.SelectedFuelItemId,
                selectedFuelItemId,
                StringComparison.Ordinal)
            || !string.Equals(expected.SourceDigest, sourceDigest,
                StringComparison.Ordinal)
            || !vectorExact)
        {
            throw new InvalidOperationException(
                "Frozen crop input authority drifted from authored semantics.");
        }
        return expected;
    }

    public static string CaptureSourceDigest(
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability,
        string excludedDestinationId,
        SurvivalWeatherType weather,
        float consumptionMultiplier,
        string selectedFuelItemId,
        IReadOnlyDictionary<string, int> requirements)
    {
        if (crop == null || ability == null || requirements == null)
            throw new ArgumentNullException(
                crop == null
                    ? nameof(crop)
                    : ability == null
                        ? nameof(ability)
                        : nameof(requirements));
        string excluded = excludedDestinationId ?? string.Empty;
        selectedFuelItemId ??= string.Empty;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(crop.CropId);
        digest.Append(crop.SeedItemId);
        digest.AppendFloat(crop.DailyWater);
        digest.AppendFloat(crop.GrowthHours);
        digest.Append(ability.Indoor);
        digest.AppendFloat(ability.WaterMultiplier);
        digest.AppendFloat(ability.CompostPerCycle);
        digest.Append(ability.FuelPerCycle);
        digest.AppendEnum(weather);
        digest.AppendFloat(consumptionMultiplier);
        digest.Append(excluded);
        digest.Append(selectedFuelItemId);
        digest.Append(requirements.Count);
        foreach (KeyValuePair<string, int> value in requirements.OrderBy(
                     value => value.Key,
                     StringComparer.Ordinal))
        {
            digest.Append(value.Key);
            digest.Append(value.Value);
        }
        return digest.ComputeSha256();
    }

    private string ResolveFuel(
        string excludedDestinationId,
        Func<string, string, int> countAvailableStock)
    {
        FacilitySupplyProfile fuelProfile = new()
        {
            kind = FacilitySupplyKind.Fuel,
            requiredTags = ResourceIngredientTag.Fuel,
            minimumValue = 0.01f
        };
        ResourceItemDefinitionSO[] candidates = catalog.Items
            .Where(fuelProfile.Allows)
            .OrderBy(
                item => item.UnitPrice / Mathf.Max(0.01f, item.FuelValue))
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Crop plot requires an authored fuel-tagged item.");
        ResourceItemDefinitionSO selected = countAvailableStock == null
            ? candidates[0]
            : candidates.FirstOrDefault(item =>
                countAvailableStock(item.ItemId, excludedDestinationId) > 0)
                ?? candidates[0];
        return selected.ItemId;
    }

    private void RequireItem(string itemId, string cropId)
    {
        if (string.IsNullOrWhiteSpace(itemId)
            || !catalog.TryGetItem(itemId, out _))
        {
            throw new InvalidOperationException(
                "Crop input authority references a missing item: "
                + cropId + "/" + itemId);
        }
    }
}
