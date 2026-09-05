using System;
using UnityEngine;

/// <summary>
/// Reproduction-owned adapter over the common durable facility-equipment
/// authority. The reproduction aggregate mutation is the effect of the same
/// transaction as breeding-ledger wear, so a rejected process creation
/// restores the exact prior durability component.
/// </summary>
public sealed class ReproductionDurableEquipmentUseRuntime
{
    public const string PlanEffectKind = "reproduction-process-plan";
    public const double LedgerWearPerPlan = 1d;

    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentSlotCommand slots;
    private readonly IDurableFacilityEquipmentUseCommand use;

    public ReproductionDurableEquipmentUseRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots,
        IDurableFacilityEquipmentUseCommand use)
    {
        this.policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.use = use ?? throw new ArgumentNullException(nameof(use));
    }

    [GameplayInternalOnly(
        "Commits one reproduction process through the registered breeding-ledger slot.",
        "ReproductionCommandRuntime only")]
    public bool TryCommitPlan(
        BuildingInstanceId facilityId,
        Vector2Int facilityPosition,
        Func<bool> commitPlan)
    {
        if (!facilityId.IsValid || commitPlan == null)
        {
            throw new ArgumentException(
                "Reproduction equipment plan input is invalid.");
        }
        if (!policies.TryGetPolicy(
                ReproductionDurableEquipmentPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The breeding-ledger durable-equipment policy is not registered.");
        }

        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            facilityId.Value,
            facilityId,
            facilityPosition);
        DurableFacilityEquipmentSlotResult reconciled = slots.TryReconcile(
            assignment);
        if (reconciled.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Breeding-ledger slot reconciliation conflicted: "
                + reconciled.FailureReason);
        }
        if (!reconciled.Succeeded)
            return false;

        DurableFacilityEquipmentSlotResult supplied = slots.TryEnsureSupply(
            assignment.Key);
        if (supplied.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Breeding-ledger supply reconciliation conflicted: "
                + supplied.FailureReason);
        }

        DurableFacilityEquipmentUseResult result = use.TryApplyWearAndEffect(
            assignment.Key,
            ReproductionDurableEquipmentPolicySource.RequirementId,
            LedgerWearPerPlan,
            new ReproductionProcessPlanEffect(commitPlan));
        return result.Succeeded;
    }

    private sealed class ReproductionProcessPlanEffect :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly Func<bool> commit;

        internal ReproductionProcessPlanEffect(Func<bool> commit)
        {
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
        }

        public string EffectKind => PlanEffectKind;

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
                    ReproductionDurableEquipmentPolicySource.PolicyId,
                    StringComparison.Ordinal)
                && string.Equals(
                    requirement.RequirementId,
                    ReproductionDurableEquipmentPolicySource.RequirementId,
                    StringComparison.Ordinal)
                && requirement.ItemId.Equals(
                    (ItemDefinitionId)DurableToolItemRules.BreedingLedger)
                && Math.Abs(wearAmount - LedgerWearPerPlan) <= 0.000001d;
            failureReason = valid
                ? string.Empty
                : "reproduction-process-plan-preflight-mismatch";
            return valid;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            if (!commit())
            {
                failureReason = "reproduction-process-plan-rejected";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }
}
