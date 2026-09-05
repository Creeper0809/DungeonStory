using System;
using UnityEngine;

/// <summary>
/// Climate adapter over the common durable facility-equipment authority.
/// Daily almanac and observation-kit wear is composed as one operation: if the
/// second tool cannot be worn, the common use runtime rolls the first tool back.
/// </summary>
public sealed class ClimateDurableEquipmentRuntime
{
    public const double AlmanacWearPerObservationDay = 0.25d;
    public const double ObservationKitWearPerObservationDay = 1d;
    public const string AlmanacEffectKind = "climate-observation-tool-pair";
    public const string ObservationKitEffectKind = "climate-observation-kit-wear";

    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentSlotCommand slots;
    private readonly IDurableFacilityEquipmentSlotQuery slotQuery;
    private readonly IDurableFacilityEquipmentUseCommand use;

    public ClimateDurableEquipmentRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots,
        IDurableFacilityEquipmentSlotQuery slotQuery,
        IDurableFacilityEquipmentUseCommand use)
    {
        this.policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.slotQuery = slotQuery
            ?? throw new ArgumentNullException(nameof(slotQuery));
        this.use = use ?? throw new ArgumentNullException(nameof(use));
    }

    public bool IsOperational(BuildingInstanceId towerId)
    {
        DurableFacilityEquipmentSlotKey key = CreateKey(towerId);
        return slotQuery.TryCapture(key, out DurableFacilityEquipmentSlotSnapshot slot)
            && slot.SupplyReady;
    }

    public bool TryMaintain(
        BuildingInstanceId towerId,
        Vector2Int towerPosition,
        bool applyDailyWear)
    {
        DurableFacilityEquipmentPolicy policy = RequirePolicy();
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            towerId.Value,
            towerId,
            towerPosition);
        DurableFacilityEquipmentSlotResult reconciled = slots.TryReconcile(
            assignment);
        ThrowOnConflict(reconciled, "reconciliation");
        if (!reconciled.Succeeded)
            return false;

        DurableFacilityEquipmentSlotResult supplied = slots.TryEnsureSupply(
            assignment.Key);
        ThrowOnConflict(supplied, "supply");
        if (!supplied.Succeeded || supplied.Snapshot?.SupplyReady != true)
            return false;
        if (!applyDailyWear)
            return true;

        DurableFacilityEquipmentUseResult result = use.TryApplyWearAndEffect(
            assignment.Key,
            ClimateDurableEquipmentPolicySource.AlmanacRequirementId,
            AlmanacWearPerObservationDay,
            new PairedObservationKitWearEffect(use, assignment.Key));
        return result.Succeeded;
    }

    private DurableFacilityEquipmentPolicy RequirePolicy()
    {
        if (!policies.TryGetPolicy(
                ClimateDurableEquipmentPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The climate observation durable-equipment policy is not registered.");
        }
        return policy;
    }

    private static DurableFacilityEquipmentSlotKey CreateKey(
        BuildingInstanceId towerId)
    {
        if (!towerId.IsValid)
        {
            throw new ArgumentException(
                "Climate observation equipment requires a valid tower identity.");
        }
        return new DurableFacilityEquipmentSlotKey(
            ClimateDurableEquipmentPolicySource.LogicalOwnerDomain,
            towerId.Value);
    }

    private static void ThrowOnConflict(
        DurableFacilityEquipmentSlotResult result,
        string operation)
    {
        if (result.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Climate observation equipment " + operation
                + " conflicted: " + result.FailureReason);
        }
    }

    private sealed class PairedObservationKitWearEffect :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly IDurableFacilityEquipmentUseCommand use;
        private readonly DurableFacilityEquipmentSlotKey key;

        internal PairedObservationKitWearEffect(
            IDurableFacilityEquipmentUseCommand use,
            DurableFacilityEquipmentSlotKey key)
        {
            this.use = use ?? throw new ArgumentNullException(nameof(use));
            this.key = key;
        }

        public string EffectKind => AlmanacEffectKind;

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
                && slot.Key.Equals(key)
                && string.Equals(
                    slot.PolicyId,
                    ClimateDurableEquipmentPolicySource.PolicyId,
                    StringComparison.Ordinal)
                && string.Equals(
                    requirement.RequirementId,
                    ClimateDurableEquipmentPolicySource.AlmanacRequirementId,
                    StringComparison.Ordinal)
                && requirement.ItemId.Equals(
                    (ItemDefinitionId)DurableToolItemRules.SeasonalAlmanac)
                && Math.Abs(wearAmount - AlmanacWearPerObservationDay)
                    <= 0.000001d;
            failureReason = valid
                ? string.Empty
                : "climate-almanac-wear-preflight-mismatch";
            return valid;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            DurableFacilityEquipmentUseResult kit = use.TryApplyWearAndEffect(
                key,
                ClimateDurableEquipmentPolicySource.ObservationKitRequirementId,
                ObservationKitWearPerObservationDay,
                new ObservationKitWearEffect(key));
            failureReason = kit.Succeeded
                ? string.Empty
                : string.IsNullOrWhiteSpace(kit.FailureReason)
                    ? "climate-observation-kit-wear-failed"
                    : kit.FailureReason;
            return kit.Succeeded;
        }
    }

    private sealed class ObservationKitWearEffect :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly DurableFacilityEquipmentSlotKey key;

        internal ObservationKitWearEffect(
            DurableFacilityEquipmentSlotKey key)
        {
            this.key = key;
        }

        public string EffectKind => ObservationKitEffectKind;

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
                && slot.Key.Equals(key)
                && string.Equals(
                    requirement.RequirementId,
                    ClimateDurableEquipmentPolicySource.ObservationKitRequirementId,
                    StringComparison.Ordinal)
                && requirement.ItemId.Equals(
                    (ItemDefinitionId)DurableToolItemRules.WeatherObservationKit)
                && Math.Abs(wearAmount - ObservationKitWearPerObservationDay)
                    <= 0.000001d;
            failureReason = valid
                ? string.Empty
                : "climate-observation-kit-wear-preflight-mismatch";
            return valid;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }
}
