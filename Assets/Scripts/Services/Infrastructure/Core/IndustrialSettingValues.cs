using System;
using System.Collections.Generic;
using UnityEngine;

public enum PowerPriority
{
    Critical = 1,
    Defense = 2,
    Essential = 3,
    Production = 4,
    Optional = 5
}

public enum ConveyorOverflowPolicy
{
    // Numeric save values are stable. Warehouse policies retain cargo on failure;
    // the historical ThenLoose names do not authorize an implicit ground drop.
    ReserveWarehouseThenLoose = 0,
    AnyCompatibleWarehouseThenLoose = 1,
    LooseOnly = 2,
    ManualApproval = 3
}

[Serializable]
public sealed class ConveyorFilterCriteria
{
    public List<string> itemIds = new List<string>();
    public List<StockCategory> stockCategories =
        new List<StockCategory>();
    public List<string> materialIds = new List<string>();
    public bool allowForbidden;
    public bool filterQuality;
    public CombatEquipmentQuality minimumQuality =
        CombatEquipmentQuality.Awful;
    public CombatEquipmentQuality maximumQuality =
        CombatEquipmentQuality.Mythic;
    public bool filterFreshness;
    [Range(0f, 1f)] public float minimumFreshness01;
    [Range(0f, 1f)] public float maximumFreshness01 = 1f;
    public bool allowContaminated = true;
}
