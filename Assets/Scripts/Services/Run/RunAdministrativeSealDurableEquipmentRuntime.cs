using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Declarative durable-equipment policy for the administration office seal.
/// Exact delivery, positive-gram capacity, persistence and terminal custody are
/// owned by the common Items runtime.
/// </summary>
public sealed class RunAdministrativeSealDurableEquipmentPolicySource :
    IDurableFacilityEquipmentPolicySource
{
    public const string PolicyId = "policy:run.v20-administrative-seal";
    public const string RequirementId = "administrative-seal";
    public const string LogicalOwnerDomain = "run.v20-administrative-seal";
    public const string StableSourceId = "run.v20-administrative-seal-equipment";

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
                        (ItemDefinitionId)DurableToolItemRules.AdministrativeSeal,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;
}

/// <summary>
/// Administration adapter over the common durable-equipment transaction. The
/// campaign effect and one point of seal wear commit together; a rejected or
/// throwing effect restores the exact prior durability component.
/// </summary>
public sealed class RunAdministrativeSealDurableEquipmentRuntime
{
    public const string EffectKind = "run-v20-administrative-resolution";
    public const double WearPerResolution = 1d;

    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentSlotCommand slots;
    private readonly IDurableFacilityEquipmentUseCommand use;

    public RunAdministrativeSealDurableEquipmentRuntime(
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
        "Commits one faction administration resolution through the registered seal slot.",
        "V20ContentResolutionService only")]
    public bool TryCommitResolution(
        BuildingInstanceId officeId,
        Vector2Int officePosition,
        Func<bool> commitResolution,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!officeId.IsValid || commitResolution == null)
            throw new ArgumentException("Administrative seal use input is invalid.");

        DurableFacilityEquipmentPolicy policy = RequirePolicy();
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            officeId.Value,
            officeId,
            officePosition);
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
        if (!supplied.Succeeded || supplied.Snapshot?.SupplyReady != true)
        {
            failureReason = supplied.FailureReason;
            return false;
        }

        DurableFacilityEquipmentUseResult result = use.TryApplyWearAndEffect(
            assignment.Key,
            RunAdministrativeSealDurableEquipmentPolicySource.RequirementId,
            WearPerResolution,
            new ResolutionEffect(commitResolution));
        failureReason = result.FailureReason;
        return result.Succeeded;
    }

    private DurableFacilityEquipmentPolicy RequirePolicy()
    {
        if (!policies.TryGetPolicy(
                RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The administrative-seal durable-equipment policy is not registered.");
        }
        return policy;
    }

    private static void ThrowOnConflict(
        DurableFacilityEquipmentSlotResult result,
        string operation)
    {
        if (result.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Administrative-seal equipment " + operation
                + " conflicted: " + result.FailureReason);
        }
    }

    private sealed class ResolutionEffect :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly Func<bool> commit;

        internal ResolutionEffect(Func<bool> commit)
        {
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
        }

        public string EffectKind => RunAdministrativeSealDurableEquipmentRuntime.EffectKind;

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
                    RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
                    StringComparison.Ordinal)
                && string.Equals(
                    requirement.RequirementId,
                    RunAdministrativeSealDurableEquipmentPolicySource.RequirementId,
                    StringComparison.Ordinal)
                && requirement.ItemId.Equals(
                    (ItemDefinitionId)DurableToolItemRules.AdministrativeSeal)
                && Math.Abs(wearAmount - WearPerResolution) <= 0.000001d;
            failureReason = valid
                ? string.Empty
                : "administrative-seal-resolution-preflight-mismatch";
            return valid;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            if (!commit())
            {
                failureReason = "administrative-seal-resolution-rejected";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }
}
