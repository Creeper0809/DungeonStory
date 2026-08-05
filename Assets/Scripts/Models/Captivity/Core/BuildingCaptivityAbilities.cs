using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("포로 수용")]
public sealed class BuildingCaptiveHousingAbility : BuildingAbility
{
    [Min(1), InspectorName("수용 인원")] public int capacity = 1;
    [Min(0), InspectorName("구속구 슬롯")] public int restraintSlots = 1;
    [Range(0f, 100f), InspectorName("기본 보안")] public float baseSecurity = 35f;
    [InspectorName("인간형 수용")] public bool acceptsHumanoids = true;

    public bool IsValid => capacity > 0 && acceptsHumanoids;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("서커스 무대")]
public sealed class BuildingCircusStageAbility : BuildingAbility
{
    [Min(1), InspectorName("동시 공연자")] public int performerCapacity = 2;
    [Min(1), InspectorName("기본 표 값")] public int baseTicketPrice = 12;
    [Min(1f), InspectorName("편성 작업량")] public float preparationWork = 12f;
    [Min(1f), InspectorName("기본 공연 시간")] public float showDurationSeconds = 45f;

    public bool IsValid => performerCapacity > 0 && showDurationSeconds > 0f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("공연 관람석")]
public sealed class BuildingAudienceSeatingAbility : BuildingAbility
{
    [Min(1), InspectorName("좌석 수")] public int capacity = 1;
    [Range(0f, 1f), InspectorName("시야 품질")] public float sightQuality = 0.8f;

    public bool IsValid => capacity > 0;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("야수 우리")]
public sealed class BuildingBeastPenAbility : BuildingAbility
{
    [Min(1), InspectorName("수용 마릿수")] public int capacity = 1;
    [Range(0f, 100f), InspectorName("기본 보안")] public float baseSecurity = 30f;
    [Min(0f), InspectorName("일일 식량")] public float dailyFood = 1f;
    [Min(0f), InspectorName("일일 물")] public float dailyWater = 1f;
    [Min(1f), InspectorName("길들이기 작업량")] public float tamingWork = 18f;
    [Min(1f), InspectorName("산출물 수거 작업량")] public float productCollectionWork = 8f;

    public bool IsValid => capacity > 0;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("서커스 매표소")]
public sealed class BuildingCircusTicketBoothAbility : BuildingAbility
{
    [Range(1f, 2f), InspectorName("표 수익 배율")] public float revenueMultiplier = 1.15f;
    [Min(0), InspectorName("관객당 추가 수익")] public int flatRevenuePerAudience = 1;

    public bool IsValid => revenueMultiplier >= 1f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("서커스 도박 창구")]
public sealed class BuildingCircusGamblingAbility : BuildingAbility
{
    [Min(0), InspectorName("관객당 도박 수익")] public int revenuePerAudience = 3;
    [Range(0f, 15f), InspectorName("만족도 변동폭")] public float satisfactionVariance = 5f;

    public bool IsValid => revenuePerAudience > 0 || satisfactionVariance > 0f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("서커스 진행자 단상")]
public sealed class BuildingCircusAnnouncerAbility : BuildingAbility
{
    [Range(0f, 20f), InspectorName("만족도 보너스")] public float satisfactionBonus = 6f;
    [Range(0.5f, 1f), InspectorName("준비 작업량 배율")] public float preparationWorkMultiplier = 0.9f;

    public bool IsValid => satisfactionBonus > 0f || preparationWorkMultiplier < 1f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("서커스 위험 장치")]
public sealed class BuildingCircusHazardAbility : BuildingAbility
{
    [Range(0f, 0.5f), InspectorName("사고 확률 증가")] public float accidentRiskBonus = 0.08f;
    [Range(0f, 20f), InspectorName("만족도 보너스")] public float satisfactionBonus = 8f;

    public bool IsValid => accidentRiskBonus > 0f || satisfactionBonus > 0f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("서커스 치료 구역")]
public sealed class BuildingCircusTreatmentZoneAbility : BuildingAbility
{
    [Range(0.25f, 1f), InspectorName("공연 사고 피해 배율")] public float accidentDamageMultiplier = 0.65f;

    public bool IsValid => accidentDamageMultiplier < 1f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("공개 형벌 장치")]
public sealed class BuildingPublicPunishmentAbility : BuildingAbility
{
    [Range(0f, 20f), InspectorName("잔혹 공연 만족도")] public float cruelSatisfactionBonus = 8f;
    [Range(1f, 3f), InspectorName("오염량 배율")] public float filthMultiplier = 1.35f;
    [Range(0f, 15f), InspectorName("직원 기분 피해")] public float witnessMoodPenalty = 5f;

    public bool IsValid => cruelSatisfactionBonus > 0f;
}
