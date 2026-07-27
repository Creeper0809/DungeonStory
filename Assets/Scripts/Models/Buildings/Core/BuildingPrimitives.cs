using System;

public enum BuildingCategory
{
    None = 0,
    Wall = 1,
    Shop = 2,
    Special = 3,
    Movement = 4,
    Production = 5,
    Crafting = 6,
    Resource = 7
}

[Flags]
public enum FacilityRole
{
    None = 0,
    Meal = 1 << 0,
    Purchase = 1 << 1,
    Rest = 1 << 2,
    Training = 1 << 3,
    Research = 1 << 4,
    Mana = 1 << 5,
    Logistics = 1 << 6,
    Toilet = 1 << 7,
    Hygiene = 1 << 8,
    Administration = 1 << 9,
    Security = 1 << 10,
    Entertainment = 1 << 11
}

[Flags]
public enum FacilityWorkType
{
    None = 0,
    Operate = 1 << 0,
    Restock = 1 << 1,
    Repair = 1 << 2,
    Clean = 1 << 3,
    Research = 1 << 4,
    Guard = 1 << 5,
    Rescue = 1 << 6,
    Rest = 1 << 7,
    Craft = 1 << 8,
    Haul = 1 << 9,
    Reception = 1 << 10,
    Hunt = 1 << 11,
    Butcher = 1 << 12,
    DrawWater = 1 << 13,
    Cook = 1 << 14,
    Treat = 1 << 15,
    Refuel = 1 << 16,
    Construct = 1 << 17,
    Warden = 1 << 18,
    Perform = 1 << 19
}

public enum StockCategory
{
    Food = 0,
    General = 1,
    Weapon = 2,
    Mana = 3,
    Water = 4,
    Medicine = 5,
    Fuel = 6,
    Ammunition = 7,
    Biological = 8,
    Knowledge = 9,
    Blueprint = 10
}
