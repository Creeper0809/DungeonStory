using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonFacilityShopSaveData
{
    public int currentOfferDay = 1;
    public List<int> basicPurchaseBuildingIds = new List<int>();
    public List<int> acquiredBlueprintIds = new List<int>();
    public List<int> unlockedBuildingIds = new List<int>();
}
