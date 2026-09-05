using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Declarative durable-equipment policy for the security post signal horn.
/// </summary>
public sealed class InvasionSignalHornDurableEquipmentPolicySource :
    IDurableFacilityEquipmentPolicySource
{
    public const string PolicyId = "policy:invasion.signal-horn";
    public const string RequirementId = "watch-signal-horn";
    public const string LogicalOwnerDomain = "invasion.signal-horn";
    public const string StableSourceId = "invasion.signal-horn-equipment";

    private static readonly IReadOnlyList<DurableFacilityEquipmentPolicy>
        Policies = Array.AsReadOnly(new[]
        {
            new DurableFacilityEquipmentPolicy(
                PolicyId,
                revision: 1L,
                LogicalOwnerDomain,
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
                new[]
                {
                    new DurableFacilityEquipmentRequirement(
                        RequirementId,
                        (ItemDefinitionId)DurableToolItemRules.WatchSignalHorn,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;
}

/// <summary>
/// Invasion adapter over the common durable-equipment slot. Availability can
/// be prepared before rally tuning; the actual wear encloses invasion publish
/// so any rejected or throwing publish restores the horn component exactly.
/// </summary>
public sealed class InvasionSignalHornDurableEquipmentRuntime
{
    public const string EffectKind = "invasion-signal-horn-rally";
    public const double WearPerRally = 1d;

    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentSlotCommand slots;
    private readonly IDurableFacilityEquipmentUseCommand use;

    public InvasionSignalHornDurableEquipmentRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots,
        IDurableFacilityEquipmentUseCommand use)
    {
        this.policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.use = use ?? throw new ArgumentNullException(nameof(use));
    }

    public bool TryEnsureReady(
        BuildingInstanceId signalPostId,
        Vector2Int signalPostPosition,
        out string failureReason)
    {
        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            signalPostId,
            signalPostPosition);
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
        failureReason = supplied.FailureReason;
        return supplied.Succeeded && supplied.Snapshot?.SupplyReady == true;
    }

    public bool TryCommitRally(
        BuildingInstanceId signalPostId,
        Vector2Int signalPostPosition,
        Func<bool> commitRally,
        out string failureReason)
    {
        if (commitRally == null)
            throw new ArgumentNullException(nameof(commitRally));
        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            signalPostId,
            signalPostPosition);
        DurableFacilityEquipmentUseResult result = use.TryApplyWearAndEffect(
            assignment.Key,
            InvasionSignalHornDurableEquipmentPolicySource.RequirementId,
            WearPerRally,
            new RallyEffect(commitRally));
        failureReason = result.FailureReason;
        return result.Succeeded;
    }

    private DurableFacilityEquipmentAssignment CreateAssignment(
        BuildingInstanceId signalPostId,
        Vector2Int signalPostPosition)
    {
        if (!signalPostId.IsValid)
            throw new ArgumentException("Signal-horn use requires a valid facility.");
        if (!policies.TryGetPolicy(
                InvasionSignalHornDurableEquipmentPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The invasion signal-horn durable-equipment policy is not registered.");
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
                "Signal-horn equipment " + operation
                + " conflicted: " + result.FailureReason);
        }
    }

    private sealed class RallyEffect : IDurableFacilityEquipmentEffectCommit
    {
        private readonly Func<bool> commit;

        internal RallyEffect(Func<bool> commit)
        {
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
        }

        public string EffectKind => InvasionSignalHornDurableEquipmentRuntime.EffectKind;

        public bool TryPreflight(
            DurableFacilityEquipmentSlotSnapshot slot,
            DurableFacilityEquipmentRequirement requirement,
            DurableFacilityEquipmentUseSubject subject,
            double wearAmount,
            out string failureReason)
        {
            bool valid = slot != null
                && requirement != null
                && subject != null
                && string.Equals(
                    slot.PolicyId,
                    InvasionSignalHornDurableEquipmentPolicySource.PolicyId,
                    StringComparison.Ordinal)
                && string.Equals(
                    requirement.RequirementId,
                    InvasionSignalHornDurableEquipmentPolicySource.RequirementId,
                    StringComparison.Ordinal)
                && requirement.ItemId.Equals(
                    (ItemDefinitionId)DurableToolItemRules.WatchSignalHorn)
                && Math.Abs(wearAmount - WearPerRally) <= 0.000001d;
            failureReason = valid
                ? string.Empty
                : "invasion-signal-horn-rally-preflight-mismatch";
            return valid;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            if (!commit())
            {
                failureReason = "invasion-signal-horn-rally-rejected";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }
}
