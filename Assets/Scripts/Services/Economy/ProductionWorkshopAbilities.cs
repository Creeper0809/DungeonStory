using System;
using UnityEngine;

public enum ProductionSupportKind
{
    Passive = 0,
    BatchProcessor = 1
}

[Serializable]
[BuildingAbilityDisplayName("생산 주 작업대")]
public sealed class BuildingProductionWorkstationAbility : BuildingAbility
{
    [InspectorName("작업대 태그")]
    public string workstationTag = string.Empty;

    public string WorkstationTag => workstationTag?.Trim() ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(WorkstationTag);
}

[Serializable]
[BuildingAbilityDisplayName("생산 보조 시설")]
public sealed class BuildingProductionSupportAbility : BuildingAbility
{
    [InspectorName("보조 시설 ID")]
    public string supportId = string.Empty;

    [InspectorName("제공 기능 태그")]
    public string[] featureTags = Array.Empty<string>();

    [InspectorName("호환 작업대 태그")]
    public string[] compatibleWorkstationTags = Array.Empty<string>();

    [InspectorName("시설 종류")]
    public ProductionSupportKind kind;

    [Min(1), InspectorName("배치 용량")]
    public int batchCapacity = 1;

    [InspectorName("전력 필요")]
    public bool requiresPower;

    [Min(0f), InspectorName("단계당 깨끗한 물")]
    public float cleanWaterPerCycle;

    [Min(0f), InspectorName("단계당 폐수")]
    public float wastewaterPerCycle;

    [InspectorName("물통 대체 허용")]
    public bool allowsManualWaterFallback;

    [InspectorName("물리 연료 필요")]
    public bool requiresFuel;

    [InspectorName("소비 연료 아이템")]
    public string fuelItemId = "resource:log";

    [Min(1), InspectorName("공정당 연료")]
    public int fuelPerCycle = 1;

    [Min(0.01f), InspectorName("작업 속도 배율")]
    public float workSpeedMultiplier = 1f;

    [Min(0.01f), InspectorName("산출량 배율")]
    public float outputMultiplier = 1f;

    [InspectorName("품질 보정")]
    public float qualityModifier;

    public string SupportId => supportId?.Trim() ?? string.Empty;
    public int BatchCapacity => kind == ProductionSupportKind.BatchProcessor
        ? Mathf.Max(1, batchCapacity)
        : 0;
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(SupportId)
        && featureTags != null
        && featureTags.Length > 0;

    public bool Provides(string featureTag)
    {
        if (string.IsNullOrWhiteSpace(featureTag) || featureTags == null)
        {
            return false;
        }

        string normalized = featureTag.Trim();
        for (int index = 0; index < featureTags.Length; index++)
        {
            if (string.Equals(
                    featureTags[index]?.Trim(),
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool SupportsWorkstation(string workstationTag)
    {
        if (string.IsNullOrWhiteSpace(workstationTag)
            || compatibleWorkstationTags == null)
        {
            return false;
        }

        string normalized = workstationTag.Trim();
        for (int index = 0; index < compatibleWorkstationTags.Length; index++)
        {
            if (string.Equals(
                    compatibleWorkstationTags[index]?.Trim(),
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public static class ProductionWorkshopAbilityAccessors
{
    public static BuildingProductionWorkstationAbility
        GetProductionWorkstationAbility(this BuildingSO building)
    {
        BuildingProductionWorkstationAbility ability =
            building?.GetAbility<BuildingProductionWorkstationAbility>();
        return ability != null && ability.IsValid ? ability : null;
    }

    public static BuildingProductionSupportAbility
        GetProductionSupportAbility(this BuildingSO building)
    {
        BuildingProductionSupportAbility ability =
            building?.GetAbility<BuildingProductionSupportAbility>();
        return ability != null && ability.IsValid ? ability : null;
    }

    public static string GetProductionWorkstationTag(this BuildableObject building)
    {
        return building?.BuildingData
            .GetProductionWorkstationAbility()?.WorkstationTag
            ?? string.Empty;
    }

    public static bool MatchesProductionWorkstation(
        this BuildableObject building,
        ProductionRecipeSO recipe)
    {
        if (building == null || recipe == null || building.BuildingData == null)
        {
            return false;
        }

        BuildingProductionWorkstationAbility workstation =
            building.BuildingData.GetProductionWorkstationAbility();
        if (workstation != null)
        {
            return string.Equals(
                workstation.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal);
        }

        // Compatibility path for V1 assets. The asset migration adds explicit
        // workstation abilities; old saves remain usable until that migration runs.
        return building.BuildingData.HasSemanticTag(recipe.FacilityTag);
    }
}
