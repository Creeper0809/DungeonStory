using System;
using System.Collections.Generic;
using System.Linq;
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

public sealed class SeasonalFeedSelfHeatingTargetQuery :
    ISeasonalFeedSelfHeatingTargetQuery
{
    private readonly IItemDefinitionCatalog itemDefinitions;
    private readonly IWorldItemStackRuntime items;
    private readonly IBuildingWorldQuery buildings;

    public SeasonalFeedSelfHeatingTargetQuery(
        IItemDefinitionCatalog itemDefinitions,
        IWorldItemStackRuntime items,
        IBuildingWorldQuery buildings)
    {
        this.itemDefinitions = itemDefinitions
            ?? throw new ArgumentNullException(nameof(itemDefinitions));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
    }

    public bool TrySelect(
        SeasonalFeedSelfHeatingFireProfile profile,
        out SeasonalFeedSelfHeatingTarget target,
        out SeasonalFeedSelfHeatingTargetFailure reason)
    {
        target = default;
        if (!TryRequireProfile(profile, out reason))
        {
            return false;
        }

        foreach (WorldItemStackSnapshot stack in
                 (items.GetAllStacks()
                     ?? Array.Empty<WorldItemStackSnapshot>())
                 .Where(value => value != null)
                 .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            if (!IsEligibleFeedStack(stack, profile))
            {
                continue;
            }

            BuildableObject facility = FindEligibleFacility(
                stack.DestinationId,
                requiredFacilityInstanceId: default);
            if (facility == null)
            {
                continue;
            }

            target = CreateTarget(stack, facility);
            reason = SeasonalFeedSelfHeatingTargetFailure.None;
            return true;
        }

        reason = new SeasonalFeedSelfHeatingTargetFailure(
            SeasonalFeedSelfHeatingTargetFailureCode.NoEligiblePhysicalStock,
            "No Stored FacilityFeedEligible stock meets the feed self-heating "
            + "minimum at an eligible environmental-fire warehouse.");
        return false;
    }

    public bool TryRevalidate(
        in SeasonalFeedSelfHeatingTarget selected,
        SeasonalFeedSelfHeatingFireProfile profile,
        out SeasonalFeedSelfHeatingTarget current,
        out SeasonalFeedSelfHeatingTargetFailure reason)
    {
        current = default;
        if (!TryRequireProfile(profile, out reason))
        {
            return false;
        }
        if (!selected.IsValid)
        {
            reason = new SeasonalFeedSelfHeatingTargetFailure(
                SeasonalFeedSelfHeatingTargetFailureCode
                    .SelectedStackMissing,
                "The selected feed self-heating target is not canonical.");
            return false;
        }

        string selectedStackId = selected.StackId;
        WorldItemStackSnapshot stack = (items.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.StackId,
                    selectedStackId,
                    StringComparison.Ordinal));
        if (stack == null)
        {
            reason = new SeasonalFeedSelfHeatingTargetFailure(
                SeasonalFeedSelfHeatingTargetFailureCode.SelectedStackMissing,
                $"Selected feed stack '{selected.StackId}' disappeared before ignition.");
            return false;
        }
        if (!string.Equals(
                stack.ItemId,
                selected.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.DestinationId,
                selected.DestinationId,
                StringComparison.Ordinal))
        {
            reason = new SeasonalFeedSelfHeatingTargetFailure(
                SeasonalFeedSelfHeatingTargetFailureCode.SelectedStackChanged,
                $"Selected feed stack '{selected.StackId}' changed item or destination before ignition.");
            return false;
        }
        if (!IsEligibleFeedStack(stack, profile))
        {
            reason = new SeasonalFeedSelfHeatingTargetFailure(
                SeasonalFeedSelfHeatingTargetFailureCode
                    .SelectedStackNotEligible,
                $"Selected feed stack '{selected.StackId}' is no longer Stored, "
                + "unreserved, FacilityFeedEligible, or above the required quantity.");
            return false;
        }

        BuildableObject facility = FindEligibleFacility(
            selected.DestinationId,
            selected.FacilityInstanceId);
        if (facility == null)
        {
            reason = new SeasonalFeedSelfHeatingTargetFailure(
                SeasonalFeedSelfHeatingTargetFailureCode
                    .SelectedFacilityUnavailable,
                $"Selected feed facility '{selected.FacilityInstanceId.Value}' "
                + "became unavailable before ignition.");
            return false;
        }

        current = CreateTarget(stack, facility);
        reason = SeasonalFeedSelfHeatingTargetFailure.None;
        return true;
    }

    private static bool TryRequireProfile(
        SeasonalFeedSelfHeatingFireProfile profile,
        out SeasonalFeedSelfHeatingTargetFailure reason)
    {
        if (profile?.IsConfigured != true)
        {
            reason = new SeasonalFeedSelfHeatingTargetFailure(
                SeasonalFeedSelfHeatingTargetFailureCode
                    .ProfileNotConfigured,
                "Feed self-heating target selection requires a configured profile.");
            return false;
        }

        IReadOnlyList<string> errors = profile.Validate(
            "seasonal-feed-self-heating-target");
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }

        reason = SeasonalFeedSelfHeatingTargetFailure.None;
        return true;
    }

    private bool IsEligibleFeedStack(
        WorldItemStackSnapshot stack,
        SeasonalFeedSelfHeatingFireProfile profile)
    {
        if (stack.State != WorldItemStackState.Stored
            || stack.ReservedQuantity != 0
            || stack.Quantity < profile.minimumPhysicalQuantity
            || string.IsNullOrWhiteSpace(stack.StackId)
            || string.IsNullOrWhiteSpace(stack.ItemId)
            || string.IsNullOrWhiteSpace(stack.DestinationId)
            || !itemDefinitions.TryGet(
                (ItemDefinitionId)stack.ItemId,
                out ItemDefinitionSO authored)
            || authored is not ResourceItemDefinitionSO resource)
        {
            return false;
        }

        return resource.FacilityFeedEligible;
    }

    private BuildableObject FindEligibleFacility(
        string destinationId,
        BuildingInstanceId requiredFacilityInstanceId)
    {
        IEnumerable<BuildableObject> candidates = (buildings.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(building => building != null
                && !building.isDestroy
                && building.PersistentInstanceId.IsValid
                && (!requiredFacilityInstanceId.IsValid
                    || building.PersistentInstanceId.Equals(
                        requiredFacilityInstanceId))
                && building is IWarehouseFacility warehouse
                && warehouse.HasWarehouseInventory
                && string.Equals(
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    destinationId,
                    StringComparison.Ordinal)
                && building.BuildingData?
                    .GetAbility<BuildingEnvironmentalFireAbility>()
                    is BuildingEnvironmentalFireAbility ability
                && ability.Accepts(
                    EnvironmentalFireIgnitionKind.FeedSelfHeating));
        return candidates
            .OrderBy(
                building => building.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static SeasonalFeedSelfHeatingTarget CreateTarget(
        WorldItemStackSnapshot stack,
        BuildableObject facility) => new(
        stack.StackId,
        stack.ItemId,
        stack.Quantity,
        stack.DestinationId,
        facility.PersistentInstanceId,
        FacilityShopService.GetBuildingName(facility.BuildingData));
}
