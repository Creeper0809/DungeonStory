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
    // 0은 일일 구매 내역을 저장하지 않던 기존 V1 형식이다.
    public int dailyOfferPurchaseStateVersion = 1;
    public List<string> purchasedDailyOfferKeys = new List<string>();
    public List<int> basicPurchaseBuildingIds = new List<int>();
    public List<int> acquiredBlueprintIds = new List<int>();
}
