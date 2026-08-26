using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("경제")]
public sealed class BuildingEconomyAbility : BuildingAbility
{
    [Min(0), InspectorName("건설 가치")] public int constructionValue;
    [Min(0), HideInInspector] public int constructionCost;
    [Min(0), InspectorName("일일 유지비")] public int maintenance;
    [Range(1, 3), InspectorName("해금 단계")] public int unlockPhase = 1;
    [Range(0f, 1f), InspectorName("철거 환급률")] public float demolitionRefundRate = 0.5f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("내부 재고")]
public sealed class BuildingInternalStockAbility : BuildingAbility
{
    [Min(0), InspectorName("최대 재고")] public int capacity;
    [Min(0), InspectorName("보충 요청 기준")] public int restockRequestThreshold;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("이용 시 재고 필요")]
public sealed class BuildingRequiresStockAbility : BuildingAbility { }

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("직원 서비스 필요")]
public sealed class BuildingStaffedServiceAbility : BuildingAbility { }

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("정식 방 필요")]
public sealed class BuildingRoomRequirementAbility : BuildingAbility { }

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("독립 공간")]
public sealed class BuildingSelfContainedRoomAbility : BuildingAbility { }

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("종족 선호")]
public sealed class BuildingSpeciesAffinityAbility : BuildingAbility
{
    [InspectorName("선호 종족 태그")]
    public string[] preferredTags = Array.Empty<string>();
    [InspectorName("기피 종족 태그")]
    public string[] dislikedTags = Array.Empty<string>();

    public bool HasAnyTag => (preferredTags?.Length ?? 0) > 0
        || (dislikedTags?.Length ?? 0) > 0;

    public bool IsPreferred(string speciesTag)
    {
        return Contains(preferredTags, speciesTag);
    }

    public bool IsDisliked(string speciesTag)
    {
        return Contains(dislikedTags, speciesTag);
    }

    private static bool Contains(IEnumerable<string> tags, string speciesTag)
    {
        return !string.IsNullOrWhiteSpace(speciesTag)
            && tags != null
            && tags.Any(tag => string.Equals(tag, speciesTag, StringComparison.OrdinalIgnoreCase));
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("의미 태그")]
public sealed class BuildingSemanticTagsAbility : BuildingAbility
{
    [InspectorName("태그 목록")]
    public string[] tags = Array.Empty<string>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("시설 등급")]
public sealed class BuildingQualityAbility : BuildingAbility
{
    [Range(1, 5), InspectorName("등급")]
    public int star = 1;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("보관")]
public sealed class BuildingStorageAbility : BuildingAbility
{
    [InspectorName("품목 분류")] public StockCategory category = StockCategory.General;
    [Min(0), InspectorName("보관량")] public int capacity;
    [InspectorName("최대 보관 질량 (g)")] public long maxStoredMassGrams;
    [InspectorName("모든 품목 허용")] public bool allCategories;

    public bool IsValid => capacity > 0 || maxStoredMassGrams > 0L;
    public bool HasMassCapacityAuthority => maxStoredMassGrams > 0L;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("좌석")]
public sealed class BuildingSeatingAbility : BuildingAbility
{
    [Min(0), InspectorName("좌석 수")] public int capacity = 1;

    public bool IsValid => capacity > 0;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("테이블")]
public sealed class BuildingTableAbility : BuildingAbility
{
    [Min(0), InspectorName("이용 인원")] public int capacity = 1;

    public bool IsValid => capacity > 0;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("서비스")]
public sealed class BuildingServiceAbility : BuildingAbility, IBuildingStockCategorySignal
{
    [Min(0), InspectorName("동시 이용 인원")] public int capacity = 1;
    public bool contributesStockCategory;
    public StockCategory stockCategory = StockCategory.General;

    public bool IsValid => capacity > 0;

    public IEnumerable<StockCategory> GetStockCategorySignals()
    {
        if (contributesStockCategory)
        {
            yield return stockCategory;
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("Equipment Maintenance")]
public sealed class BuildingEquipmentMaintenanceAbility : BuildingAbility
{
    [Min(0.1f)] public float workSpeedMultiplier = 1f;
    [Min(1)] public int simultaneousRepairSlots = 1;
    [SerializeField] private string repairSupplyItemId = string.Empty;
    [Min(1), SerializeField] private int repairSupplyPerQuarterDurability = 1;

    public string RepairSupplyItemId =>
        repairSupplyItemId?.Trim() ?? string.Empty;
    public int RepairSupplyPerQuarterDurability =>
        Mathf.Max(1, repairSupplyPerQuarterDurability);

#if UNITY_EDITOR
    public void ConfigureRepairSupply(
        string itemId,
        int amountPerQuarterDurability)
    {
        repairSupplyItemId = itemId?.Trim() ?? string.Empty;
        repairSupplyPerQuarterDurability =
            Mathf.Max(1, amountPerQuarterDurability);
    }
#endif
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("Facility Part")]
public sealed class BuildingFacilityPartAbility : BuildingAbility
{
    public string code;
    public bool IsValid => !string.IsNullOrWhiteSpace(code);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("판매")]
public sealed class BuildingRetailAbility : BuildingAbility, IBuildingStockCategorySignal
{
    public StockCategory category = StockCategory.General;

    public IEnumerable<StockCategory> GetStockCategorySignals()
    {
        yield return category;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("청소")]
public sealed class BuildingCleaningAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [Range(0f, 100f)] public float restoredCleanliness = 100f;

}
