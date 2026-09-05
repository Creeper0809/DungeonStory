using System;
using System.Collections.Generic;
using System.Linq;
using VContainer.Unity;

/// <summary>
/// Closes the two event-tool slots when their exact facility disappears or no
/// longer exposes the authored role. The common slot runtime owns physical
/// drain, carried recovery, save joins and checkpoint collection.
/// </summary>
public sealed class RunInvasionDurableEquipmentLifecycleRuntime :
    IStartable,
    ITickable,
    IDungeonSaveCaptureGuard
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IDurableFacilityEquipmentSlotQuery slots;
    private readonly IDurableFacilityEquipmentSlotCommand commands;
    private string unresolvedFailure = string.Empty;

    public RunInvasionDurableEquipmentLifecycleRuntime(
        IBuildingWorldQuery buildings,
        IDurableFacilityEquipmentSlotQuery slots,
        IDurableFacilityEquipmentSlotCommand commands)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public string GuardId => "run-invasion-durable-equipment-lifecycle";

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
            "Run/invasion durable-equipment lifecycle has an unresolved conflict: "
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
                "run-invasion-durable-equipment-live-facility-id-duplicate";
            return;
        }
        Dictionary<string, BuildableObject> byId = live.ToDictionary(
            value => value.RequirePersistentInstanceId().Value,
            StringComparer.Ordinal);

        foreach (DurableFacilityEquipmentSlotSnapshot slot in slots.CaptureAll()
                     .Where(value => value != null
                         && value.LifecyclePhase ==
                         DurableFacilityEquipmentSlotLifecyclePhase.Active
                         && IsOwnedPolicy(value.PolicyId))
                     .OrderBy(value => value.AssignmentSequence))
        {
            if (!byId.TryGetValue(
                    slot.OwnerFacilityId.Value,
                    out BuildableObject facility))
            {
                Close(slot, "durable-event-tool-facility-lost");
                continue;
            }
            if (!SupportsExpectedRole(slot.PolicyId, facility))
                Close(slot, "durable-event-tool-capability-removed");
        }
    }

    internal static bool SupportsExpectedRole(
        string policyId,
        BuildableObject facility)
    {
        if (facility == null || facility.isDestroy || facility.BuildingData == null)
            return false;
        if (string.Equals(
                policyId,
                RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
                StringComparison.Ordinal))
        {
            return facility.SupportsFacilityRole(FacilityRole.Administration);
        }
        if (string.Equals(
                policyId,
                InvasionSignalHornDurableEquipmentPolicySource.PolicyId,
                StringComparison.Ordinal))
        {
            return facility.SupportsFacilityRole(FacilityRole.Security);
        }
        return false;
    }

    private static bool IsOwnedPolicy(string policyId) =>
        string.Equals(
            policyId,
            RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
            StringComparison.Ordinal)
        || string.Equals(
            policyId,
            InvasionSignalHornDurableEquipmentPolicySource.PolicyId,
            StringComparison.Ordinal);

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
                ? "run-invasion-durable-equipment-lifecycle-close-conflict"
                : result.FailureReason;
        }
    }
}
