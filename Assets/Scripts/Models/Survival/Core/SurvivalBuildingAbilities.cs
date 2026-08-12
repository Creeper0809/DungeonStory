using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("급수")]
public sealed class BuildingWaterSourceAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [Min(1), InspectorName("작업당 물 생산량")]
    public int waterPerWork = 4;
    [Min(0.1f), InspectorName("필요 작업량")]
    public float workSeconds = 1f;
    [InspectorName("동결 날씨에 사용 불가")]
    public bool blockedByFreezingWeather = true;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("조리")]
public sealed class BuildingCookingAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [Min(1), InspectorName("입력 식량")]
    public int inputFood = 1;
    [Min(1), InspectorName("완성 식사")]
    public int cookedMeals = 2;
    [Min(0.1f), InspectorName("필요 작업량")]
    public float workSeconds = 1.2f;
    [InspectorName("연료 필요")]
    public bool requiresFuel = true;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("식량 보존")]
public sealed class BuildingPreservationAbility : BuildingAbility
{
    [Range(1f, 8f), InspectorName("신선도 배율")]
    public float freshnessMultiplier = 4f;
    [Min(1), InspectorName("조리당 보존 식량")]
    public int preservedMealsPerCook = 1;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("의료")]
public sealed class BuildingMedicalAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [Min(0.1f), InspectorName("필요 작업량")]
    public float workSeconds = 1.4f;
    [Min(0f), InspectorName("중증도 감소")]
    public float severityReduction = 0.45f;
    [InspectorName("약품 필요")]
    public bool requiresMedicine = true;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("연료 소비")]
public sealed class BuildingFuelConsumerAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [Min(1), InspectorName("보충당 연료")]
    public int fuelPerRefuel = 1;
    [Min(0.1f), InspectorName("필요 작업량")]
    public float workSeconds = 0.8f;
    [Min(0f), InspectorName("난방")]
    public float warmth = 8f;
    [Min(0f), InspectorName("조명 안전")]
    public float lightSafety = 10f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("골렘 충전")]
public sealed class BuildingGolemRechargeAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    public string materialItemId = "resource:mana-crystal";
    [Min(1)] public int materialQuantity = 1;
    [Min(0.1f)] public float requiredWork = 100f;
    [Min(0.1f)] public float restoredCharge = 50f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("온도 조절")]
public sealed class BuildingTemperatureAbility : BuildingAbility
{
    [InspectorName("방 온도 보정")]
    public float roomTemperatureOffset = 4f;
    [Min(0f), InspectorName("추위 보호")]
    public float coldProtection = 8f;
    [Min(0f), InspectorName("더위 보호")]
    public float heatProtection = 4f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("환기")]
public sealed class BuildingVentilationAbility : BuildingAbility
{
    [Range(0f, 100f), InspectorName("위생 위험 감소")]
    public float hygieneRiskReduction = 10f;
    [Range(0f, 100f), InspectorName("연기 위험 감소")]
    public float smokeRiskReduction = 20f;
}
