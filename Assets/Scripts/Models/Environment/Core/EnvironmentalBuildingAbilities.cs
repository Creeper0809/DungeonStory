using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public enum ThermalEmitterMode
{
    Heat = 0,
    Cool = 1,
    Thermostat = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("환경 열원")]
public sealed class BuildingThermalEmitterAbility : BuildingAbility
{
    [InspectorName("동작 방식")]
    public ThermalEmitterMode mode = ThermalEmitterMode.Heat;

    [Range(-20f, 45f), InspectorName("목표 온도")]
    public float targetTemperatureC = 20f;

    [InspectorName("플레이어 목표 온도 설정")]
    public bool playerConfigurable;

    [Range(-20f, 45f), InspectorName("설정 최저 온도")]
    public float minimumTargetTemperatureC = -20f;

    [Range(-20f, 45f), InspectorName("설정 최고 온도")]
    public float maximumTargetTemperatureC = 45f;

    [Min(0f), InspectorName("초당 열 교환")]
    public float degreesPerSecond = 2f;

    [Min(0), InspectorName("작용 반경")]
    public int radius = 2;

    [InspectorName("전력 필요")]
    public bool requiresPower;

    [InspectorName("배출 셀 오프셋")]
    public Vector2Int exhaustOffset = Vector2Int.right;

    [Range(0f, 2f), InspectorName("배출 열 배율")]
    public float exhaustHeatMultiplier = 1.15f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("환경 공기 교환")]
public sealed class BuildingAirExchangeAbility : BuildingAbility
{
    [Range(0f, 100f), InspectorName("목표 공기질")]
    public float targetAirQuality = 100f;

    [Min(0f), InspectorName("초당 정화량")]
    public float qualityPerSecond = 8f;

    [Min(0), InspectorName("작용 반경")]
    public int radius = 3;

    [InspectorName("전력 필요")]
    public bool requiresPower = true;

    [InspectorName("외부 공기 사용")]
    public bool exchangesWithOutside = true;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("환경 공조 덕트")]
public sealed class BuildingAirDuctAbility : BuildingAbility
{
    [Range(0.1f, 1f), InspectorName("셀 교환율")]
    public float exchangeRate = 0.65f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("환경 보호장비 보관")]
public sealed class BuildingProtectiveEquipmentLockerAbility : BuildingAbility
{
    [Min(1), InspectorName("보관 슬롯")]
    public int capacity = 4;

    [Min(0), InspectorName("보호 구역 반경")]
    public int serviceRadius = 12;
}
