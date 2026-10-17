using System;
using DungeonStory.Environment;
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

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("환경 화재 프로필")]
public sealed class BuildingEnvironmentalFireAbility : BuildingAbility
{
    [InspectorName("허용 발화 원인")]
    public EnvironmentalFireIgnitionSources acceptedSources =
        EnvironmentalFireIgnitionSources.None;

    [Min(0.01f), InspectorName("가연 연료량")]
    public float fuelCapacity = 1f;
    [Range(0.01f, 1f), InspectorName("최대 화재 강도")]
    public float maximumIntensity = 0.8f;
    [Range(0f, 1f), InspectorName("틱당 성장")]
    public float growthPerTick = 0.05f;
    [Min(0.01f), InspectorName("틱당 연료 소모")]
    public float fuelConsumedPerTick = 0.1f;
    [Min(0.01f), InspectorName("틱당 구조 피해")]
    public float damagePerTick = 12f;
    [Range(0f, 1f), InspectorName("확산 최소 강도")]
    public float minimumSpreadIntensity = 0.55f;
    [Range(0f, 1f), InspectorName("틱당 확산 확률")]
    public float spreadChancePerTick = 0.1f;
    [Range(0f, 1f), InspectorName("확산 발화 강도 배율")]
    public float spreadIgnitionMultiplier = 0.5f;
    [Min(0.01f), InspectorName("물 1개당 진화 강도")]
    public float waterSuppressionPerUnit = 0.5f;

    [Min(0f), InspectorName("전기 발화 최소 Heat")]
    public float electricalIgnitionHeat = 90f;
    [Min(0f), InspectorName("전기 발화 최소 Fault")]
    public float electricalIgnitionFault = 5f;
    [Min(0.01f), InspectorName("전기 발화 판정 창(게임초)")]
    public float electricalIgnitionWindowSeconds = 30f;
    [Range(0f, 1f), InspectorName("전기 창당 발화 확률")]
    public float electricalIgnitionChancePerWindow = 0.15f;
    [Range(0.01f, 1f), InspectorName("전기 초기 강도")]
    public float electricalIgnitionIntensity = 0.25f;

    public bool Accepts(EnvironmentalFireIgnitionKind kind)
    {
        if (!Enum.IsDefined(typeof(EnvironmentalFireIgnitionKind), kind))
            return false;
        var source = (EnvironmentalFireIgnitionSources)(1 << (int)kind);
        return (acceptedSources & source) != 0;
    }

    public EnvironmentalFireProfile CreateProfileOrThrow()
    {
        if ((acceptedSources & ~EnvironmentalFireIgnitionSources.All) != 0
            || acceptedSources == EnvironmentalFireIgnitionSources.None)
        {
            throw new InvalidOperationException(
                $"{nameof(BuildingEnvironmentalFireAbility)} has invalid ignition sources.");
        }
        RequireFiniteNonNegative(
            electricalIgnitionHeat,
            nameof(electricalIgnitionHeat));
        RequireFiniteNonNegative(
            electricalIgnitionFault,
            nameof(electricalIgnitionFault));
        RequireFinitePositive(
            electricalIgnitionWindowSeconds,
            nameof(electricalIgnitionWindowSeconds));
        RequireFiniteRange(
            electricalIgnitionChancePerWindow,
            0f,
            1f,
            nameof(electricalIgnitionChancePerWindow));
        RequireFiniteRange(
            electricalIgnitionIntensity,
            0.01f,
            maximumIntensity,
            nameof(electricalIgnitionIntensity));

        return new EnvironmentalFireProfile(
            fuelCapacity,
            maximumIntensity,
            growthPerTick,
            fuelConsumedPerTick,
            damagePerTick,
            minimumSpreadIntensity,
            spreadChancePerTick,
            spreadIgnitionMultiplier,
            waterSuppressionPerUnit);
    }

    private static void RequireFinitePositive(float value, string field)
    {
        if (!IsFinite(value) || value <= 0f)
            throw new InvalidOperationException(
                $"{nameof(BuildingEnvironmentalFireAbility)}.{field} must be finite and positive.");
    }

    private static void RequireFiniteNonNegative(float value, string field)
    {
        if (!IsFinite(value) || value < 0f)
            throw new InvalidOperationException(
                $"{nameof(BuildingEnvironmentalFireAbility)}.{field} must be finite and nonnegative.");
    }

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        string field)
    {
        if (!IsFinite(value) || value < minimum || value > maximum)
            throw new InvalidOperationException(
                $"{nameof(BuildingEnvironmentalFireAbility)}.{field} is outside its valid range.");
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("가동 열원 화재 위험")]
public sealed class BuildingActiveHeatFireSourceAbility : BuildingAbility
{
    [InspectorName("통전 필요")]
    public bool requiresPower = true;
    [InspectorName("현재 시설 사용 필요")]
    public bool requiresActiveUser = true;
    [Min(0.01f), InspectorName("발화 판정 창(게임초)")]
    public float ignitionWindowSeconds = 30f;
    [Range(0f, 1f), InspectorName("창당 발화 확률")]
    public float ignitionChancePerWindow = 0.15f;
    [Range(0.01f, 1f), InspectorName("초기 강도")]
    public float ignitionIntensity = 0.25f;

    public void ValidateOrThrow()
    {
        RequireFinitePositive(ignitionWindowSeconds, nameof(ignitionWindowSeconds));
        RequireFiniteRange(
            ignitionChancePerWindow,
            0f,
            1f,
            nameof(ignitionChancePerWindow));
        RequireFiniteRange(ignitionIntensity, 0.01f, 1f, nameof(ignitionIntensity));
    }

    private static void RequireFinitePositive(float value, string field)
    {
        if (!IsFinite(value) || value <= 0f)
            throw new InvalidOperationException(
                $"{nameof(BuildingActiveHeatFireSourceAbility)}.{field} must be finite and positive.");
    }

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        string field)
    {
        if (!IsFinite(value) || value < minimum || value > maximum)
            throw new InvalidOperationException(
                $"{nameof(BuildingActiveHeatFireSourceAbility)}.{field} is outside its valid range.");
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("공정 사고 화재 결과")]
public sealed class BuildingProcessAccidentFireSourceAbility : BuildingAbility
{
    [InspectorName("화재 결과 작업 ID")]
    public string workTypeId = string.Empty;
    [Range(0.01f, 1f), InspectorName("초기 강도")]
    public float ignitionIntensity = 0.25f;

    public bool Supports(string candidateWorkTypeId) =>
        !string.IsNullOrWhiteSpace(candidateWorkTypeId)
        && string.Equals(
            workTypeId?.Trim() ?? string.Empty,
            candidateWorkTypeId.Trim(),
            StringComparison.Ordinal);

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(workTypeId)
            || !string.Equals(workTypeId, workTypeId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{nameof(BuildingProcessAccidentFireSourceAbility)} requires a canonical work type ID.");
        }
        if (float.IsNaN(ignitionIntensity)
            || float.IsInfinity(ignitionIntensity)
            || ignitionIntensity < 0.01f
            || ignitionIntensity > 1f)
        {
            throw new InvalidOperationException(
                $"{nameof(BuildingProcessAccidentFireSourceAbility)}.{nameof(ignitionIntensity)} is outside its valid range.");
        }
    }
}
