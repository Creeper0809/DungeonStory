using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonFacilityShopSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int currentOfferDay = 1;
    public List<int> basicPurchaseBuildingIds = new List<int>();
    public List<int> acquiredBlueprintIds = new List<int>();
}
