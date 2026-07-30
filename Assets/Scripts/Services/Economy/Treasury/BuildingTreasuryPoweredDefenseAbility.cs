using System;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("금고 연동 방어")]
public sealed class BuildingTreasuryPoweredDefenseAbility : BuildingAbility
{
    [Min(1), InspectorName("발사 비용")]
    public int shotCost = 30;

    [Min(0), InspectorName("최소 위협도")]
    public int defaultMinimumThreat;

    [Min(0), InspectorName("침공당 기본 예산")]
    public int defaultInvasionBudget = 300;

    [InspectorName("보스 전용")]
    public bool defaultBossOnly;

    public bool IsValid => shotCost > 0;
}
