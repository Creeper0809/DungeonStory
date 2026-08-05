public readonly struct OffenseRareFacilityCandidateSnapshot
{
    public OffenseRareFacilityCandidateSnapshot(
        int sourceIndex,
        int buildingId,
        int star,
        bool isGridMovement,
        bool isWall)
    {
        SourceIndex = sourceIndex;
        BuildingId = buildingId;
        Star = star;
        IsGridMovement = isGridMovement;
        IsWall = isWall;
    }

    public int SourceIndex { get; }
    public int BuildingId { get; }
    public int Star { get; }
    public bool IsGridMovement { get; }
    public bool IsWall { get; }
}

public readonly struct OffenseBlueprintCandidateSnapshot
{
    public OffenseBlueprintCandidateSnapshot(
        int sourceIndex,
        int blueprintId,
        int rarity,
        bool isEligible,
        bool isRewardAcquired,
        bool isShopAcquired,
        bool isResearchCompleted)
    {
        SourceIndex = sourceIndex;
        BlueprintId = blueprintId;
        Rarity = rarity;
        IsEligible = isEligible;
        IsRewardAcquired = isRewardAcquired;
        IsShopAcquired = isShopAcquired;
        IsResearchCompleted = isResearchCompleted;
    }

    public int SourceIndex { get; }
    public int BlueprintId { get; }
    public int Rarity { get; }
    public bool IsEligible { get; }
    public bool IsRewardAcquired { get; }
    public bool IsShopAcquired { get; }
    public bool IsResearchCompleted { get; }
}
