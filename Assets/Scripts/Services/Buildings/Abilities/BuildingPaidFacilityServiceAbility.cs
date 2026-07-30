using System;
using UnityEngine;

public enum PaidFacilityChargeMode
{
    PerUse = 0,
    PerOrder = 1,
    DailyContract = 2
}

[Serializable]
[BuildingAbilityDisplayName("유료 운영")]
public sealed class BuildingPaidFacilityServiceAbility : BuildingAbility
{
    [InspectorName("결제 방식")]
    public PaidFacilityChargeMode chargeMode = PaidFacilityChargeMode.PerUse;

    [Min(0), InspectorName("비용")]
    public int cost;

    [InspectorName("계약 ID")]
    public string contractId = string.Empty;

    [InspectorName("표시 이름")]
    public string displayName = "유료 시설 서비스";
}
