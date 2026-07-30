using System;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("기반 시설 연결")]
public sealed class BuildingUtilityConnectionAbility : BuildingAbility
{
    [InspectorName("연결 채널")]
    public UtilityChannel channels =
        UtilityChannel.Power
        | UtilityChannel.CleanWater
        | UtilityChannel.Wastewater;

    [Min(0.1f), InspectorName("최대 처리량")]
    public float maxThroughput = 100f;

    [InspectorName("기본 개방")]
    public bool normallyOpen = true;
}

[Serializable]
[BuildingAbilityDisplayName("전력 생산")]
public sealed class BuildingPowerProducerAbility : BuildingAbility
{
    [Min(0f), InspectorName("초당 발전량")]
    public float productionPerSecond = 10f;

    [InspectorName("연료 필요")]
    public bool requiresFuel;

    [InspectorName("연료 아이템 ID")]
    public string fuelItemId = "stock-item:Fuel";

    [Min(0f), InspectorName("연료당 가동 시간")]
    public float secondsPerFuel = 60f;
}

[Serializable]
[BuildingAbilityDisplayName("전력 소비")]
public sealed class BuildingPowerConsumerAbility : BuildingAbility
{
    [Min(0f), InspectorName("초당 소비량")]
    public float demandPerSecond = 1f;

    [InspectorName("기본 우선순위")]
    public PowerPriority priority = PowerPriority.Production;

    [Range(0f, 1f), InspectorName("최소 공급 비율")]
    public float minimumSupplyFraction = 1f;
}

[Serializable]
[BuildingAbilityDisplayName("축전")]
public sealed class BuildingPowerStorageAbility : BuildingAbility
{
    [Min(0f), InspectorName("저장 용량")]
    public float capacity = 100f;

    [Min(0f), InspectorName("최대 충전/방전")]
    public float transferPerSecond = 20f;

    [Range(0f, 1f), InspectorName("충방전 효율")]
    public float efficiency = 0.92f;
}

[Serializable]
[BuildingAbilityDisplayName("회로 차단기")]
public sealed class BuildingCircuitBreakerAbility : BuildingAbility
{
    [Min(1f), InspectorName("과부하 허용률")]
    public float overloadTolerance = 1.15f;

    [Min(1f), InspectorName("차단 열 임계값")]
    public float tripHeat = 100f;
}

[Serializable]
[BuildingAbilityDisplayName("상수 생산")]
public sealed class BuildingWaterProducerAbility : BuildingAbility
{
    public WorldWaterQuality quality = WorldWaterQuality.Clean;

    [Min(0f), InspectorName("초당 생산량")]
    public float productionPerSecond = 1f;

    [InspectorName("전력 필요")]
    public bool requiresPower = true;
}

[Serializable]
[BuildingAbilityDisplayName("유체 저장")]
public sealed class BuildingWaterStorageAbility : BuildingAbility
{
    [InspectorName("저장 채널")]
    public UtilityChannel channels =
        UtilityChannel.CleanWater | UtilityChannel.Wastewater;

    [Min(0f), InspectorName("상수 용량")]
    public float cleanWaterCapacity = 50f;

    [Min(0f), InspectorName("오수 용량")]
    public float wastewaterCapacity = 50f;
}

[Serializable]
[BuildingAbilityDisplayName("물통 충전")]
public sealed class BuildingWaterContainerTransferAbility : BuildingAbility
{
    [Min(0.1f), InspectorName("배치당 물")]
    public float waterPerBatch = 1f;

    [Min(0.1f), InspectorName("배치 시간")]
    public float secondsPerBatch = 4f;

    [Min(1), InspectorName("병입 목표 재고")]
    public int bottleTargetStock = 10;

    [InspectorName("전력 필요")]
    public bool requiresPower = true;
}

[Serializable]
[BuildingAbilityDisplayName("급배수 시설")]
public sealed class BuildingWaterFixtureAbility : BuildingAbility
{
    [Min(0f), InspectorName("사용당 상수")]
    public float cleanWaterPerUse = 0.25f;

    [Min(0f), InspectorName("사용당 폐수")]
    public float wastewaterPerUse = 0.25f;

    [InspectorName("최저 수질")]
    public WorldWaterQuality minimumQuality = WorldWaterQuality.Clean;

    [InspectorName("수동 물통 대체")]
    public bool allowsManualWaterFallback = true;

    [InspectorName("재래식 대체")]
    public bool allowsDryFallback;

    [InspectorName("수동 폐기물 아이템 ID")]
    public string manualWasteItemId = string.Empty;
}

[Serializable]
[BuildingAbilityDisplayName("공정용 급배수")]
public sealed class BuildingProcessFluidAbility : BuildingAbility
{
    [InspectorName("적용 작업 ID")]
    public string[] workTypeIds = Array.Empty<string>();

    [Min(0f), InspectorName("주기당 상수")]
    public float cleanWaterPerCycle = 0.25f;

    [Min(0f), InspectorName("주기당 폐수")]
    public float wastewaterPerCycle = 0.25f;

    [InspectorName("최소 수질")]
    public WorldWaterQuality minimumQuality = WorldWaterQuality.Clean;

    [InspectorName("물통 수동 보충 허용")]
    public bool allowsManualWaterFallback = true;

    public bool Supports(WorkTypeId workTypeId)
    {
        if (!workTypeId.IsValid || workTypeIds == null)
        {
            return false;
        }

        string value = workTypeId.Value;
        for (int i = 0; i < workTypeIds.Length; i++)
        {
            if (string.Equals(
                    workTypeIds[i]?.Trim(),
                    value,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

[Serializable]
[BuildingAbilityDisplayName("폐수 처리")]
public sealed class BuildingWastewaterProcessorAbility : BuildingAbility
{
    [Min(0.1f), InspectorName("회당 폐수 입력")]
    public float wastewaterInput = 10f;

    [Min(0f), InspectorName("회당 물 출력")]
    public float waterOutput = 6f;

    [InspectorName("출력 수질")]
    public WorldWaterQuality outputQuality = WorldWaterQuality.Unsafe;

    [InspectorName("전력 필요")]
    public bool requiresPower = true;

    [InspectorName("슬러지 아이템 ID")]
    public string sludgeItemId = "industrial:sludge";

    [Min(0), InspectorName("슬러지 출력")]
    public int sludgeAmount = 1;

    [Min(0.1f), InspectorName("회당 처리 시간")]
    public float secondsPerBatch = 10f;
}

[Serializable]
[BuildingAbilityDisplayName("컨베이어 구간")]
public sealed class BuildingConveyorSegmentAbility : BuildingAbility
{
    [Min(0.1f), InspectorName("이동 속도")]
    public float speed = 1f;

    [Min(1), InspectorName("구간 적재 수")]
    public int capacity = 1;

    [InspectorName("출력 방향")]
    public Vector2Int[] outputDirections = { Vector2Int.right };

    [InspectorName("전력 필요")]
    public bool requiresPower = true;

    [InspectorName("필터 아이템 ID")]
    public string[] allowedItemIds = Array.Empty<string>();

    [InspectorName("필터 재고 분류")]
    public StockCategory[] allowedStockCategories =
        Array.Empty<StockCategory>();

    [InspectorName("필터 재질 ID")]
    public string[] allowedMaterialIds = Array.Empty<string>();

    [InspectorName("품질 필터")]
    public bool filterQuality;

    [InspectorName("최소 품질")]
    public CombatEquipmentQuality minimumQuality =
        CombatEquipmentQuality.Awful;

    [InspectorName("최대 품질")]
    public CombatEquipmentQuality maximumQuality =
        CombatEquipmentQuality.Legendary;

    [InspectorName("신선도 필터")]
    public bool filterFreshness;

    [Range(0f, 1f), InspectorName("최소 신선도")]
    public float minimumFreshness01;

    [Range(0f, 1f), InspectorName("최대 신선도")]
    public float maximumFreshness01 = 1f;

    [InspectorName("오염 허용")]
    public bool allowContaminated = true;

    [InspectorName("금지 아이템 허용")]
    public bool allowForbidden;
}

[Serializable]
[BuildingAbilityDisplayName("컨베이어 포트")]
public sealed class BuildingConveyorPortAbility : BuildingAbility
{
    public ConveyorPortMode mode = ConveyorPortMode.Both;

    [InspectorName("목적지 ID")]
    public string destinationId = string.Empty;

    [Min(1), InspectorName("포트 버퍼")]
    public int capacity = 4;
}

[Serializable]
[BuildingAbilityDisplayName("오버플로 배출")]
public sealed class BuildingConveyorOverflowAbility : BuildingAbility
{
    public ConveyorOverflowPolicy defaultPolicy =
        ConveyorOverflowPolicy.ReserveWarehouseThenLoose;

    [Min(1f), InspectorName("배출 대기 시간")]
    public float stallSeconds = 30f;
}

[Serializable]
[BuildingAbilityDisplayName("생산 자동화")]
public sealed class BuildingAutomationAbility : BuildingAbility
{
    public AutomationMode maximumMode = AutomationMode.Automatic;

    [Min(0f), InspectorName("보조 전력")]
    public float assistedPowerDemand = 2f;

    [Min(0f), InspectorName("자동 전력")]
    public float automaticPowerDemand = 5f;

    [Min(0.01f), InspectorName("보조 작업 배율")]
    public float assistedWorkMultiplier = 1.35f;

    [Min(0.01f), InspectorName("자동 초당 작업량")]
    public float automaticWorkPerSecond = 1f;

    [Range(0f, 1f), InspectorName("자동 품질 상한")]
    public float automaticQualityCap = 0.75f;

    [Min(0f), InspectorName("시간당 유지보수 소모")]
    public float maintenancePerGameHour = 1f;
}
