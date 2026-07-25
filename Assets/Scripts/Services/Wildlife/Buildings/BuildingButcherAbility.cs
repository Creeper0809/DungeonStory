using System;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("도축")]
public sealed class BuildingButcherAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [Min(0.1f), InspectorName("필요 작업량")]
    public float workSeconds = 1f;
}
