using System;
using System.Collections.Generic;
using System.Linq;
using VContainer.Unity;

/// <summary>
/// Closes climate observation equipment slots when their exact authored tower
/// disappears or no longer owns the weather-observation capability. The common
/// durable slot runtime performs the carried-aware physical drain and save join.
/// </summary>
public sealed class ClimateDurableEquipmentLifecycleRuntime :
    IStartable,
    ITickable,
    IDungeonSaveCaptureGuard
{
    public const int WeatherTowerDefinitionId = 8851;

    private readonly IBuildingWorldQuery buildings;
    private readonly IDurableFacilityEquipmentSlotQuery slots;
    private readonly IDurableFacilityEquipmentSlotCommand commands;
    private string unresolvedFailure = string.Empty;

    public ClimateDurableEquipmentLifecycleRuntime(
        IBuildingWorldQuery buildings,
        IDurableFacilityEquipmentSlotQuery slots,
        IDurableFacilityEquipmentSlotCommand commands)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public string GuardId => "infrastructure.climate-durable-equipment-lifecycle";

    public void Start() => ReconcileLostOwners();

    public void Tick() => ReconcileLostOwners();

    public bool CanCapture(out string failureReason)
    {
        ReconcileLostOwners();
        failureReason = unresolvedFailure;
        return failureReason.Length == 0;
    }

    public void ValidateBeforeCapture()
    {
        if (CanCapture(out string failureReason))
            return;
        throw new InvalidOperationException(
            "Climate durable-equipment lifecycle has an unresolved conflict: "
            + failureReason);
    }

    private void ReconcileLostOwners()
    {
        unresolvedFailure = string.Empty;
        BuildableObject[] live = (buildings.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null && !value.isDestroy)
            .OrderBy(value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToArray();
        if (live.Select(value => value.RequirePersistentInstanceId().Value)
                .Distinct(StringComparer.Ordinal).Count() != live.Length)
        {
            unresolvedFailure =
                "climate-durable-equipment-live-facility-id-duplicate";
            return;
        }
        Dictionary<string, BuildableObject> byId = live.ToDictionary(
            value => value.RequirePersistentInstanceId().Value,
            StringComparer.Ordinal);

        foreach (DurableFacilityEquipmentSlotSnapshot slot in slots.CaptureAll()
                     .Where(value => value != null
                         && value.LifecyclePhase ==
                         DurableFacilityEquipmentSlotLifecyclePhase.Active
                         && string.Equals(
                             value.PolicyId,
                             ClimateDurableEquipmentPolicySource.PolicyId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.AssignmentSequence))
        {
            if (!byId.TryGetValue(
                    slot.OwnerFacilityId.Value,
                    out BuildableObject facility))
            {
                Close(slot, "climate-observation-tower-lost");
                continue;
            }
            if (!IsWeatherObservationTower(facility))
            {
                Close(slot, "climate-observation-capability-removed");
            }
        }
    }

    internal static bool IsWeatherObservationTower(BuildableObject facility) =>
        facility != null
        && !facility.isDestroy
        && facility.BuildingData != null
        && facility.BuildingData.id == WeatherTowerDefinitionId;

    private void Close(
        DurableFacilityEquipmentSlotSnapshot slot,
        string reasonCode)
    {
        DurableFacilityEquipmentSlotResult result = commands.TryClose(
            slot.Key,
            reasonCode);
        if (result.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            unresolvedFailure = string.IsNullOrWhiteSpace(result.FailureReason)
                ? "climate-durable-equipment-lifecycle-close-conflict"
                : result.FailureReason;
        }
    }
}
