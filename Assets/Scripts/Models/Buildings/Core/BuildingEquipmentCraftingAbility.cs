using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("장비 제작")]
public sealed class BuildingEquipmentCraftingAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility,
    IBuildingEquipmentCraftingDefinition
{
    [InspectorName("제작 가능한 장비 ID")]
    public string[] craftableEquipmentIds = Array.Empty<string>();

    [Min(0.1f), InspectorName("제작 작업량")]
    [FormerlySerializedAs("workSecondsPerCycle")]
    public float workUnitsPerCycle = 1f;

    public IReadOnlyList<string> CraftableEquipmentIds =>
        craftableEquipmentIds ?? Array.Empty<string>();
}
