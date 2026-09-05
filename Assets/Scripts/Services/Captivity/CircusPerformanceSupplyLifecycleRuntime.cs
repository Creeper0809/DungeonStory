using System;
using System.Collections.Generic;
using System.Linq;
using VContainer.Unity;

/// <summary>
/// Retires stage-owned performance supply slots when the persistent stage is
/// lost or no longer exposes the circus-stage capability. The common slot
/// runtime performs the exact carried-aware custody drain and owns its save
/// continuation; this guard prevents capture while that close conflicts.
/// </summary>
public sealed class CircusPerformanceSupplyLifecycleRuntime :
    IStartable,
    ITickable,
    IDungeonSaveCaptureGuard
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IDurableFacilityEquipmentSlotQuery slots;
    private readonly IDurableFacilityEquipmentSlotCommand commands;
    private string unresolvedFailure = string.Empty;

    public CircusPerformanceSupplyLifecycleRuntime(
        IBuildingWorldQuery buildings,
        IDurableFacilityEquipmentSlotQuery slots,
        IDurableFacilityEquipmentSlotCommand commands)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
    }

    public string GuardId => "captivity-circus-performance-supply-lifecycle";

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
            "Circus performance-supply lifecycle has an unresolved conflict: "
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
                "circus-performance-supply-live-stage-id-duplicate";
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
                             CircusPerformanceSupplyPolicySource.PolicyId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.AssignmentSequence))
        {
            if (!byId.TryGetValue(
                    slot.OwnerFacilityId.Value,
                    out BuildableObject stage))
            {
                Close(slot, "circus-performance-stage-lost");
                continue;
            }
            if (!SupportsCircusStage(stage))
                Close(slot, "circus-performance-stage-capability-removed");
        }
    }

    internal static bool SupportsCircusStage(BuildableObject stage) =>
        stage != null
        && !stage.isDestroy
        && stage.BuildingData?.GetCircusStageAbility() is { IsValid: true };

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
                ? "circus-performance-supply-close-conflict"
                : result.FailureReason;
        }
    }
}
