using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("경작지")]
public sealed class BuildingCropPlotAbility : BuildingAbility
{
    [SerializeField] private bool indoor;
    [Min(0.1f), SerializeField] private float growthMultiplier = 1f;
    [Min(0f), SerializeField] private float waterMultiplier = 1f;
    [Min(0), SerializeField] private int compostPerCycle;
    [Min(0), SerializeField] private int fuelPerCycle;
    [SerializeField] private List<ItemAmountDefinition> cycleSupplyInputs =
        new List<ItemAmountDefinition>();

    public bool Indoor => indoor;
    public float GrowthMultiplier => Mathf.Max(0.1f, growthMultiplier);
    public float WaterMultiplier => Mathf.Max(0f, waterMultiplier);
    public int CompostPerCycle => Mathf.Max(0, compostPerCycle);
    public int FuelPerCycle => Mathf.Max(0, fuelPerCycle);
    public IReadOnlyList<ItemAmountDefinition> CycleSupplyInputs =>
        cycleSupplyInputs ??= new List<ItemAmountDefinition>();

#if UNITY_EDITOR
    public void Configure(
        bool isIndoor,
        float growthRate,
        float waterRate,
        int compost,
        int fuel,
        IEnumerable<ItemAmountDefinition> supplies = null)
    {
        indoor = isIndoor;
        growthMultiplier = Mathf.Max(0.1f, growthRate);
        waterMultiplier = Mathf.Max(0f, waterRate);
        compostPerCycle = Mathf.Max(0, compost);
        fuelPerCycle = Mathf.Max(0, fuel);
        cycleSupplyInputs = (supplies ?? Array.Empty<ItemAmountDefinition>())
            .Where(input => input != null && input.HasCanonicalAuthoredValue)
            .GroupBy(input => input.ItemId, StringComparer.Ordinal)
            .Select(group => new ItemAmountDefinition(
                group.Key,
                group.Sum(input => input.Amount)))
            .OrderBy(input => input.ItemId, StringComparer.Ordinal)
            .ToList();
    }
#endif
}
