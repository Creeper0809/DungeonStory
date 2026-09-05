using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CropCycleInputRequirementAuthorityDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Production/Validate Crop Cycle Input Requirements")]
    public static void Validate()
    {
        AssetContentSource content = new();
        ResourceEconomyContentCatalog catalog = new(
            content.GetAll<ResourceItemDefinitionSO>(),
            content.GetAll<ProductionRecipeSO>(),
            content.GetAll<CropDefinitionSO>(),
            content.GetAll<CraftMaterialDefinitionSO>());
        CropCycleInputRequirementAuthority authority = new(catalog);
        CropDefinitionSO crop = catalog.Crops
            .OrderByDescending(value => value.DailyWater * value.GrowthHours)
            .ThenBy(value => value.CropId, StringComparer.Ordinal)
            .First();

        BuildingCropPlotAbility outdoor = new();
        outdoor.Configure(
            isIndoor: false,
            growthRate: 1f,
            waterRate: 1f,
            compost: 1,
            fuel: 0,
            supplies: new[]
            {
                new ItemAmountDefinition(crop.SeedItemId, 2)
            });
        CropCycleInputRequirementSnapshot dry = authority.Capture(
            crop,
            outdoor,
            "crop-input|building%3Aqa",
            SurvivalWeatherType.Clear,
            1f,
            countAvailableStock: null);
        CropCycleInputRequirementSnapshot rain = authority.Capture(
            crop,
            outdoor,
            "crop-input|building%3Aqa",
            SurvivalWeatherType.Rain,
            1f,
            countAvailableStock: null);
        CropCycleInputRequirementSnapshot clamped = authority.Capture(
            crop,
            outdoor,
            "crop-input|building%3Aqa",
            SurvivalWeatherType.Clear,
            0.01f,
            countAvailableStock: null);
        CropCycleInputRequirementSnapshot repeat = authority.Capture(
            crop,
            outdoor,
            "crop-input|building%3Aqa",
            SurvivalWeatherType.Clear,
            1f,
            countAvailableStock: null);

        Require(dry.Requirements[crop.SeedItemId] == 3,
            "Authored cycle supply did not merge with the physical seed lot.");
        int dryWater = dry.Requirements[
            CropCycleInputRequirementAuthority.CleanWaterItemId];
        int rainWater = rain.Requirements[
            CropCycleInputRequirementAuthority.CleanWaterItemId];
        Require(dryWater > rainWater
                && rainWater == Mathf.Max(
                    1,
                    Mathf.CeilToInt(dryWater * 0.5f)),
            "Outdoor rain did not apply the shared half-water requirement.");
        Require(clamped.MilestoneConsumptionMultiplier == 0.1f,
            "Crop consumption multiplier did not preserve its lower clamp.");
        Require(dry.SourceDigest == repeat.SourceDigest
                && dry.Requirements.OrderBy(value => value.Key,
                        StringComparer.Ordinal)
                    .SequenceEqual(repeat.Requirements.OrderBy(
                        value => value.Key,
                        StringComparer.Ordinal)),
            "Crop input authority is not deterministic.");

        BuildingCropPlotAbility indoor = new();
        indoor.Configure(
            isIndoor: true,
            growthRate: 1f,
            waterRate: 1f,
            compost: 1,
            fuel: 1);
        ResourceItemDefinitionSO[] fuels = catalog.Items
            .Where(new FacilitySupplyProfile
            {
                kind = FacilitySupplyKind.Fuel,
                requiredTags = ResourceIngredientTag.Fuel,
                minimumValue = 0.01f
            }.Allows)
            .OrderBy(item => item.UnitPrice / Mathf.Max(0.01f, item.FuelValue))
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
        Require(fuels.Length >= 2,
            "Crop fuel selection regression requires two authored fuels.");
        string availableFuel = fuels[1].ItemId;
        CropCycleInputRequirementSnapshot indoorClear = authority.Capture(
            crop,
            indoor,
            "crop-input|building%3Aqa",
            SurvivalWeatherType.Clear,
            1f,
            (itemId, _) => string.Equals(itemId, availableFuel,
                StringComparison.Ordinal) ? 1 : 0);
        CropCycleInputRequirementSnapshot indoorRain = authority.Capture(
            crop,
            indoor,
            "crop-input|building%3Aqa",
            SurvivalWeatherType.Rain,
            1f,
            (itemId, _) => string.Equals(itemId, availableFuel,
                StringComparison.Ordinal) ? 1 : 0);
        Require(indoorClear.SelectedFuelItemId == availableFuel
                && indoorClear.Requirements[availableFuel] == 1,
            "Crop input authority did not choose the cheapest available fuel.");
        Require(indoorClear.Requirements[
                    CropCycleInputRequirementAuthority.CleanWaterItemId]
                == indoorRain.Requirements[
                    CropCycleInputRequirementAuthority.CleanWaterItemId],
            "Indoor crop input incorrectly inherited outdoor rain relief.");
        Require(indoorClear.Requirements[
                    CropCycleInputRequirementAuthority.CompostItemId] == 1,
            "Crop compost requirement drifted from the shared authority.");
        CropCycleInputRequirementSnapshot rehydrated =
            authority.RehydrateAndValidate(
                crop,
                indoor,
                "crop-input|building%3Aqa",
                indoorClear.Weather,
                indoorClear.MilestoneConsumptionMultiplier,
                indoorClear.SelectedFuelItemId,
                indoorClear.Requirements,
                indoorClear.SourceDigest);
        Require(rehydrated.SourceDigest == indoorClear.SourceDigest,
            "Frozen crop input rehydration changed the source authority.");
        Dictionary<string, int> tampered = new(
            indoorClear.Requirements,
            StringComparer.Ordinal)
        {
            [indoorClear.SelectedFuelItemId] = 2
        };
        RequireThrows(() => authority.RehydrateAndValidate(
                crop,
                indoor,
                "crop-input|building%3Aqa",
                indoorClear.Weather,
                indoorClear.MilestoneConsumptionMultiplier,
                indoorClear.SelectedFuelItemId,
                tampered,
                indoorClear.SourceDigest),
            "Tampered frozen crop input vector was accepted.");
        RequireThrows(() => authority.RehydrateAndValidate(
                crop,
                indoor,
                "crop-input|building%3Aqa",
                SurvivalWeatherType.Rain,
                indoorClear.MilestoneConsumptionMultiplier,
                indoorClear.SelectedFuelItemId,
                indoorClear.Requirements,
                indoorClear.SourceDigest),
            "Frozen crop input weather provenance drift was accepted.");

        Debug.Log(
            "CROP_CYCLE_INPUT_REQUIREMENT_AUTHORITY_PASS "
            + $"crop={crop.CropId};dryWater={dryWater};"
            + $"rainWater={rainWater};fuel={availableFuel};"
            + $"digest={dry.SourceDigest}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class AssetContentSource : IGameContentDefinitionSource
    {
        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name,
                    new[] { "Assets/Resources/SO" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToArray();

        public T RequireSingle<T>() where T : ScriptableObject =>
            GetAll<T>().Single();
    }
}
