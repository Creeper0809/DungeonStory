using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Identifies the authored alliance signal post without treating every
/// Security facility as an equivalent physical destination.
/// </summary>
public static class AllianceSignalPostEligibility
{
    public const string RequiredSemanticTag =
        "research:defense:alliance-signals";

    public static bool IsEligibleDefinition(BuildingSO definition) =>
        definition != null
        && definition.Facility?.SupportsRole(FacilityRole.Security) == true
        && definition.HasSemanticTag(RequiredSemanticTag);

    public static bool IsEligible(BuildableObject building) =>
        building != null
        && !building.isDestroy
        && IsEligibleDefinition(building.BuildingData);

    public static BuildableObject SelectFirst(
        IEnumerable<BuildableObject> candidates) =>
        (candidates ?? Array.Empty<BuildableObject>())
        .Where(IsEligible)
        .OrderBy(
            value => value.RequirePersistentInstanceId().Value,
            StringComparer.Ordinal)
        .FirstOrDefault();
}

/// <summary>
/// Declares one exact physical alliance signal kit for a live signal post.
/// The common slot runtime owns the claim, positive gram profile, delivery
/// intent/save join, and terminal custody drain. The Run milestone aggregate
/// remains the authority for the eventual typed Sink receipt and support day.
/// </summary>
public sealed class InvasionDefenseKitSupplyPolicySource :
    IDurableFacilityEquipmentPolicySource
{
    public const string PolicyId = "policy:invasion.defense-kit";
    public const string RequirementId = "alliance-signal-kit";
    public const string LogicalOwnerDomain = "invasion.defense-kit";
    public const string StableSourceId = "invasion.defense-kit-supply";
    public const string ConsumableUsabilityPolicyKind =
        "invasion.defense-kit-consumable";
    public const string ItemId = "supply:alliance-signal-kit";

    private static readonly IReadOnlyList<DurableFacilityEquipmentPolicy>
        Policies = Array.AsReadOnly(new[]
        {
            new DurableFacilityEquipmentPolicy(
                PolicyId,
                revision: 1L,
                LogicalOwnerDomain,
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                ConsumableUsabilityPolicyKind,
                new[]
                {
                    new DurableFacilityEquipmentRequirement(
                        RequirementId,
                        (ItemDefinitionId)ItemId,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;
}

/// <summary>
/// A defense kit is usable solely by exact definition and positive quantity;
/// it has no invented durability component. Consumption is still committed by
/// the existing Run milestone typed Sink transaction.
/// </summary>
public sealed class InvasionDefenseKitSupplyUsabilityPolicy :
    IDurableFacilityEquipmentUsabilityPolicy
{
    public string PolicyKind =>
        InvasionDefenseKitSupplyPolicySource.ConsumableUsabilityPolicyKind;

    public DurableFacilityEquipmentUsabilityResult Evaluate(
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject)
    {
        if (requirement == null || subject == null)
        {
            throw new ArgumentNullException(
                requirement == null ? nameof(requirement) : nameof(subject));
        }
        if (!requirement.ItemId.Equals(
                (ItemDefinitionId)InvasionDefenseKitSupplyPolicySource.ItemId)
            || !requirement.ItemId.Equals(subject.ItemId))
        {
            return new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Incompatible,
                "invasion-defense-kit-definition-mismatch");
        }
        return subject.Quantity > 0
            ? new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Usable,
                "invasion-defense-kit-usable")
            : new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Exhausted,
                "invasion-defense-kit-empty");
    }
}

/// <summary>
/// Invasion adapter over the common exact physical slot. It only ensures and
/// observes supply; it never consumes the kit or owns the milestone outcome.
/// </summary>
public sealed class InvasionDefenseKitSupplyRuntime
{
    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentSlotCommand slots;

    public InvasionDefenseKitSupplyRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots)
    {
        this.policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
    }

    public bool TryEnsureReady(
        BuildableObject signalPost,
        out string destinationId,
        out string failureReason)
    {
        destinationId = string.Empty;
        failureReason = string.Empty;
        if (!AllianceSignalPostEligibility.IsEligible(signalPost))
        {
            failureReason = "invasion-defense-kit-signal-post-invalid";
            return false;
        }

        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            signalPost.RequirePersistentInstanceId(),
            signalPost.centerPos);
        DurableFacilityEquipmentSlotResult reconciled = slots.TryReconcile(
            assignment);
        ThrowOnConflict(reconciled, "reconciliation");
        if (!reconciled.Succeeded)
        {
            failureReason = reconciled.FailureReason;
            return false;
        }

        DurableFacilityEquipmentSlotResult supplied = slots.TryEnsureSupply(
            assignment.Key);
        ThrowOnConflict(supplied, "supply");
        destinationId = supplied.Snapshot?.DestinationId ?? string.Empty;
        failureReason = supplied.FailureReason;
        return supplied.Succeeded
            && supplied.Snapshot?.SupplyReady == true
            && destinationId.Length > 0;
    }

    private DurableFacilityEquipmentAssignment CreateAssignment(
        BuildingInstanceId signalPostId,
        Vector2Int signalPostPosition)
    {
        if (!signalPostId.IsValid)
            throw new ArgumentException(
                "Defense-kit supply requires a valid signal-post ID.");
        if (!policies.TryGetPolicy(
                InvasionDefenseKitSupplyPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The invasion defense-kit supply policy is not registered.");
        }
        return policy.CreateAssignment(
            signalPostId.Value,
            signalPostId,
            signalPostPosition);
    }

    private static void ThrowOnConflict(
        DurableFacilityEquipmentSlotResult result,
        string operation)
    {
        if (result.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Defense-kit supply " + operation
                + " conflicted: " + result.FailureReason);
        }
    }
}

/// <summary>
/// Closes an exact defense-kit slot when its signal post is destroyed or loses
/// the authored alliance-signal capability. The common slot command performs
/// the carried-aware physical release before retiring claim/profile authority.
/// </summary>
public sealed class InvasionDefenseKitSupplyLifecycleRuntime :
    IStartable,
    ITickable,
    IDungeonSaveCaptureGuard
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IDurableFacilityEquipmentSlotQuery slots;
    private readonly IDurableFacilityEquipmentSlotCommand commands;
    private string unresolvedFailure = string.Empty;

    public InvasionDefenseKitSupplyLifecycleRuntime(
        IBuildingWorldQuery buildings,
        IDurableFacilityEquipmentSlotQuery slots,
        IDurableFacilityEquipmentSlotCommand commands)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public string GuardId => "invasion-defense-kit-supply-lifecycle";

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
            "Defense-kit supply lifecycle has an unresolved conflict: "
            + failureReason);
    }

    private void ReconcileLostOwners()
    {
        unresolvedFailure = string.Empty;
        BuildableObject[] live = (buildings.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null && !value.isDestroy)
            .OrderBy(
                value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToArray();
        if (live.Select(value => value.RequirePersistentInstanceId().Value)
                .Distinct(StringComparer.Ordinal).Count() != live.Length)
        {
            unresolvedFailure =
                "invasion-defense-kit-live-facility-id-duplicate";
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
                             InvasionDefenseKitSupplyPolicySource.PolicyId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.AssignmentSequence))
        {
            if (!byId.TryGetValue(
                    slot.OwnerFacilityId.Value,
                    out BuildableObject signalPost))
            {
                Close(slot, "invasion-defense-kit-signal-post-lost");
                continue;
            }
            if (!AllianceSignalPostEligibility.IsEligible(signalPost))
            {
                Close(slot, "invasion-defense-kit-capability-removed");
            }
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
            unresolvedFailure = string.IsNullOrWhiteSpace(result.FailureReason)
                ? "invasion-defense-kit-lifecycle-close-conflict"
                : result.FailureReason;
        }
    }
}
