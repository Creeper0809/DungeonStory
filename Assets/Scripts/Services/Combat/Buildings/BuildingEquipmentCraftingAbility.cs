using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("장비 제작")]
public sealed class BuildingEquipmentCraftingAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [InspectorName("제작 가능한 장비 ID")]
    public string[] craftableEquipmentIds = Array.Empty<string>();

    [Min(0.1f), InspectorName("제작 작업량")]
    public float workSecondsPerCycle = 1f;

    public IReadOnlyList<string> CraftableEquipmentIds =>
        craftableEquipmentIds ?? Array.Empty<string>();
}
