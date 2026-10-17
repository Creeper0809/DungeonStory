using System;
using DungeonStory.Environment;


public enum SeasonalFeedSelfHeatingTargetFailureCode
{
    None = 0,
    ProfileNotConfigured = 1,
    NoEligiblePhysicalStock = 2,
    SelectedStackMissing = 3,
    SelectedStackChanged = 4,
    SelectedStackNotEligible = 5,
    SelectedFacilityUnavailable = 6
}

public readonly struct SeasonalFeedSelfHeatingTargetFailure
{
    public SeasonalFeedSelfHeatingTargetFailure(
        SeasonalFeedSelfHeatingTargetFailureCode code,
        string reason)
    {
        Code = code;
        Reason = reason?.Trim() ?? string.Empty;
    }

    public SeasonalFeedSelfHeatingTargetFailureCode Code { get; }
    public string Reason { get; }
    public bool IsFailure =>
        Code != SeasonalFeedSelfHeatingTargetFailureCode.None;

    public static SeasonalFeedSelfHeatingTargetFailure None => new(
        SeasonalFeedSelfHeatingTargetFailureCode.None,
        string.Empty);
}

public readonly struct SeasonalFeedSelfHeatingTarget
{
    public SeasonalFeedSelfHeatingTarget(
        string stackId,
        string itemId,
        int quantity,
        string destinationId,
        BuildingInstanceId facilityInstanceId,
        string facilityDisplayName)
    {
        StackId = stackId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        Quantity = quantity;
        DestinationId = destinationId?.Trim() ?? string.Empty;
        FacilityInstanceId = facilityInstanceId;
        FacilityDisplayName = facilityDisplayName?.Trim() ?? string.Empty;
    }

    public string StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public string DestinationId { get; }
    public BuildingInstanceId FacilityInstanceId { get; }
    public string FacilityDisplayName { get; }
    public bool IsValid =>
        !string.IsNullOrEmpty(StackId)
        && !string.IsNullOrEmpty(ItemId)
        && Quantity > 0
        && !string.IsNullOrEmpty(DestinationId)
        && FacilityInstanceId.IsValid
        && !string.IsNullOrEmpty(FacilityDisplayName);
}

public interface ISeasonalFeedSelfHeatingTargetQuery
{
    bool TrySelect(
        SeasonalFeedSelfHeatingFireProfile profile,
        out SeasonalFeedSelfHeatingTarget target,
        out SeasonalFeedSelfHeatingTargetFailure reason);

    bool TryRevalidate(
        in SeasonalFeedSelfHeatingTarget selected,
        SeasonalFeedSelfHeatingFireProfile profile,
        out SeasonalFeedSelfHeatingTarget current,
        out SeasonalFeedSelfHeatingTargetFailure reason);
}
