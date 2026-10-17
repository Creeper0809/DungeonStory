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
    // Zero is the valid value for saves written before purchase identities
    // were owned by the facility-shop aggregate.
    public int purchaseSequence;
    public List<int> basicPurchaseBuildingIds = new List<int>();
    public List<int> acquiredBlueprintIds = new List<int>();
}
