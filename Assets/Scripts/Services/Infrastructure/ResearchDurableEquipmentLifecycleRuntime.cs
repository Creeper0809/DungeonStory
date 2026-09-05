using System;
using System.Collections.Generic;
using System.Linq;
using VContainer.Unity;

public sealed class ResearchDurableEquipmentLifecycleRuntime :
    IStartable,
    ITickable,
    IDungeonSaveCaptureGuard
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IResearchDurableEquipmentWorkPolicyQuery workPolicies;
    private readonly IDurableFacilityEquipmentSlotQuery slots;
    private readonly IDurableFacilityEquipmentSlotCommand commands;
    private readonly Dictionary<string, Subscription> subscriptions =
        new(StringComparer.Ordinal);
    private int observedBuildingVersion = int.MinValue;
    private string unresolvedFailure = string.Empty;

    public ResearchDurableEquipmentLifecycleRuntime(
        IBuildingWorldQuery buildings,
        IResearchDurableEquipmentWorkPolicyQuery workPolicies,
        IDurableFacilityEquipmentSlotQuery slots,
        IDurableFacilityEquipmentSlotCommand commands)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.workPolicies = workPolicies
            ?? throw new ArgumentNullException(nameof(workPolicies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public string GuardId => "research.durable-equipment-lifecycle";

    public void Start()
    {
        RefreshSubscriptions();
        ReconcileLostOwners();
    }

    public void Tick()
    {
        if (observedBuildingVersion != buildings.BuildingVersion)
            RefreshSubscriptions();
        ReconcileLostOwners();
    }

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
            "Research durable-equipment lifecycle has an unresolved conflict: "
            + failureReason);
    }

    private void RefreshSubscriptions()
    {
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
                "research-durable-equipment-live-facility-id-duplicate";
            return;
        }

        HashSet<string> currentIds = new(StringComparer.Ordinal);
        foreach (BuildableObject facility in live)
        {
            if (!workPolicies.TryResolve(
                    facility,
                    out _,
                    out _))
            {
                continue;
            }
            string facilityId = facility.RequirePersistentInstanceId().Value;
            currentIds.Add(facilityId);
            if (subscriptions.TryGetValue(facilityId, out Subscription existing)
                && ReferenceEquals(existing.Facility, facility))
            {
                continue;
            }
            if (existing != null)
                existing.Detach();
            Subscription created = new(
                facility,
                () => OnFacilityDestroyed(facilityId));
            created.Attach();
            subscriptions[facilityId] = created;
        }

        foreach (string removed in subscriptions.Keys
                     .Where(value => !currentIds.Contains(value))
                     .ToArray())
        {
            subscriptions[removed].Detach();
            subscriptions.Remove(removed);
        }
        observedBuildingVersion = buildings.BuildingVersion;
    }

    private void OnFacilityDestroyed(string facilityId)
    {
        CloseFacilitySlots(facilityId, "research-facility-destroyed");
    }

    private void ReconcileLostOwners()
    {
        unresolvedFailure = string.Empty;
        Dictionary<string, BuildableObject> live = (buildings.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null && !value.isDestroy)
            .ToDictionary(
                value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal);
        foreach (DurableFacilityEquipmentSlotSnapshot slot in slots.CaptureAll()
                     .Where(value => value != null
                         && value.LifecyclePhase ==
                         DurableFacilityEquipmentSlotLifecyclePhase.Active
                         && workPolicies.IsRegisteredEquipmentPolicy(
                             value.PolicyId))
                     .OrderBy(value => value.AssignmentSequence))
        {
            if (!live.TryGetValue(
                    slot.OwnerFacilityId.Value,
                    out BuildableObject facility))
            {
                Close(slot, "research-facility-lost");
                continue;
            }
            if (!workPolicies.TryResolve(
                    facility,
                    out ResearchDurableEquipmentWorkPolicy expected,
                    out _)
                || !string.Equals(
                    expected.EquipmentPolicyId,
                    slot.PolicyId,
                    StringComparison.Ordinal))
            {
                Close(slot, "research-facility-capability-removed");
            }
        }
    }

    private void CloseFacilitySlots(string facilityId, string reasonCode)
    {
        foreach (DurableFacilityEquipmentSlotSnapshot slot in slots.CaptureAll()
                     .Where(value => value != null
                         && value.LifecyclePhase ==
                         DurableFacilityEquipmentSlotLifecyclePhase.Active
                         && string.Equals(
                             value.OwnerFacilityId.Value,
                             facilityId,
                             StringComparison.Ordinal)
                         && workPolicies.IsRegisteredEquipmentPolicy(
                             value.PolicyId))
                     .OrderBy(value => value.AssignmentSequence))
        {
            Close(slot, reasonCode);
        }
    }

    private void Close(
        DurableFacilityEquipmentSlotSnapshot slot,
        string reasonCode)
    {
        DurableFacilityEquipmentSlotResult result = commands.TryClose(
            slot.Key,
            reasonCode);
        if (result.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            unresolvedFailure = Canonical(result.FailureReason)
                ? result.FailureReason
                : "research-durable-equipment-lifecycle-close-conflict";
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private sealed class Subscription
    {
        private readonly Action destroyed;
        private bool attached;

        internal Subscription(BuildableObject facility, Action destroyed)
        {
            Facility = facility ?? throw new ArgumentNullException(nameof(facility));
            this.destroyed = destroyed ?? throw new ArgumentNullException(nameof(destroyed));
        }

        internal BuildableObject Facility { get; }

        internal void Attach()
        {
            if (attached)
                return;
            Facility.OnBuildingDestroyed += destroyed;
            attached = true;
        }

        internal void Detach()
        {
            if (!attached || Facility == null)
                return;
            Facility.OnBuildingDestroyed -= destroyed;
            attached = false;
        }
    }
}
