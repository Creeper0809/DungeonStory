using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Captivity-owned declaration for the exact physical supply pair used by one
/// circus stage. The common durable-equipment runtime owns the exact claim,
/// positive gram profile, delivery commitments, save join and carried-aware
/// terminal drain for both the consumable prop box and durable banquet cart.
/// </summary>
public sealed class CircusPerformanceSupplyPolicySource :
    IDurableFacilityEquipmentPolicySource
{
    public const string PolicyId = "policy:captivity.circus-performance-supplies";
    public const string PropBoxRequirementId = "performance-prop-box";
    public const string BanquetCartRequirementId = "banquet-cart";
    public const string LogicalOwnerDomain = "captivity.circus";
    public const string StableSourceId = "captivity.circus-performance-supplies";
    public const string MixedUsabilityPolicyKind =
        "captivity.circus-prop-and-durable-cart";

    private static readonly IReadOnlyList<DurableFacilityEquipmentPolicy>
        Policies = Array.AsReadOnly(new[]
        {
            new DurableFacilityEquipmentPolicy(
                PolicyId,
                revision: 1L,
                LogicalOwnerDomain,
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                MixedUsabilityPolicyKind,
                new[]
                {
                    new DurableFacilityEquipmentRequirement(
                        PropBoxRequirementId,
                        (ItemDefinitionId)CircusPerformanceSupplyContracts
                            .PerformancePropBoxItemId,
                        requiredQuantity: 1),
                    new DurableFacilityEquipmentRequirement(
                        BanquetCartRequirementId,
                        (ItemDefinitionId)DurableToolItemRules.BanquetCart,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;
}

/// <summary>
/// The stage slot deliberately owns one consumable and one durable item. The
/// prop is usable when its exact definition is present; the cart additionally
/// requires a positive canonical durability component.
/// </summary>
public sealed class CircusPerformanceSupplyUsabilityPolicy :
    IDurableFacilityEquipmentUsabilityPolicy
{
    private readonly PositiveDurabilityComponentUsabilityPolicy durable = new();

    public string PolicyKind =>
        CircusPerformanceSupplyPolicySource.MixedUsabilityPolicyKind;

    public DurableFacilityEquipmentUsabilityResult Evaluate(
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject)
    {
        if (requirement == null || subject == null)
            throw new ArgumentNullException(
                requirement == null ? nameof(requirement) : nameof(subject));
        if (!requirement.ItemId.Equals(subject.ItemId))
        {
            return new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Incompatible,
                "circus-performance-supply-definition-mismatch");
        }
        if (requirement.ItemId.Equals(
                (ItemDefinitionId)CircusPerformanceSupplyContracts
                    .PerformancePropBoxItemId))
        {
            return subject.Quantity > 0
                ? new DurableFacilityEquipmentUsabilityResult(
                    DurableFacilityEquipmentUsabilityDisposition.Usable,
                    "circus-performance-prop-box-usable")
                : new DurableFacilityEquipmentUsabilityResult(
                    DurableFacilityEquipmentUsabilityDisposition.Exhausted,
                    "circus-performance-prop-box-empty");
        }
        if (requirement.ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.BanquetCart))
        {
            return durable.Evaluate(requirement, subject);
        }
        return new DurableFacilityEquipmentUsabilityResult(
            DurableFacilityEquipmentUsabilityDisposition.Incompatible,
            "circus-performance-supply-definition-unsupported");
    }
}

/// <summary>
/// Wear is legal only for the banquet-cart requirement. The common positive
/// durability projector remains the arithmetic authority; this adapter only
/// retags its immutable projection with the mixed slot's registered policy.
/// </summary>
public sealed class CircusPerformanceSupplyWearPolicy :
    IDurableFacilityEquipmentWearPolicy
{
    private readonly PositiveDurabilityComponentWearPolicy durable = new();

    public string PolicyKind =>
        CircusPerformanceSupplyPolicySource.MixedUsabilityPolicyKind;

    public DurableFacilityEquipmentWearProjection Project(
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        double wearAmount)
    {
        if (requirement == null
            || !requirement.ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.BanquetCart))
        {
            throw new InvalidOperationException(
                "Only the circus banquet cart can receive durable wear.");
        }
        DurableFacilityEquipmentWearProjection projected = durable.Project(
            requirement,
            subject,
            wearAmount);
        return new DurableFacilityEquipmentWearProjection(
            PolicyKind,
            projected.ReplacementComponent,
            projected.ExhaustedAfter,
            projected.CurrentBefore,
            projected.CurrentAfter);
    }
}

/// <summary>
/// Circus adapter over the common exact-slot/use transaction. Banquet-cart
/// wear is published before the prop Sink effect; if the Sink rejects or
/// throws, the common use runtime restores the exact prior cart component.
/// </summary>
public sealed class CircusPerformanceSupplyRuntime
{
    public const double BanquetCartWearPerShow =
        CircusPerformanceSupplyContracts.BanquetCartWearPerShow;
    public const string EffectKind = "circus-performance-prop-sink";

    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentSlotCommand slots;
    private readonly IDurableFacilityEquipmentSlotQuery slotQuery;
    private readonly IDurableFacilityEquipmentUseCommand use;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService dispositions;

    public CircusPerformanceSupplyRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots,
        IDurableFacilityEquipmentSlotQuery slotQuery,
        IDurableFacilityEquipmentUseCommand use,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService dispositions)
    {
        this.policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.slotQuery = slotQuery
            ?? throw new ArgumentNullException(nameof(slotQuery));
        this.use = use ?? throw new ArgumentNullException(nameof(use));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));
    }

    public bool TryCommitShowSupplies(
        CircusShowOrder order,
        out string status)
    {
        status = string.Empty;
        if (order == null
            || string.IsNullOrWhiteSpace(order.stageId))
        {
            status = "circus-performance-supply-order-invalid";
            return false;
        }
        if (CircusShowSupplyOutbox.HasPending(order))
        {
            return CircusShowSupplyOutbox.TryFinalize(
                order,
                items,
                dispositions,
                out status);
        }
        if (order.preparationSuppliesCommitted)
            return true;

        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            (BuildingInstanceId)order.stageId,
            order.stagePosition);
        DurableFacilityEquipmentSlotResult reconciled = slots.TryReconcile(
            assignment);
        ThrowOnConflict(reconciled, "reconciliation");
        if (!reconciled.Succeeded)
        {
            status = reconciled.FailureReason;
            return false;
        }
        DurableFacilityEquipmentSlotResult supplied = slots.TryEnsureSupply(
            assignment.Key);
        ThrowOnConflict(supplied, "supply");
        if (!supplied.Succeeded || supplied.Snapshot?.SupplyReady != true)
        {
            status = "공연 준비품 배송 대기: 공연 소품 상자, 연회 운반 수레";
            return false;
        }
        if (!slotQuery.TryCapture(
                assignment.Key,
                out DurableFacilityEquipmentSlotSnapshot slot)
            || slot.LifecyclePhase !=
                DurableFacilityEquipmentSlotLifecyclePhase.Active)
        {
            status = "circus-performance-supply-slot-unavailable";
            return false;
        }

        WorldItemStackSnapshot[] props = items.GetAllStacks()
            .Where(value => value != null
                && value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(value.DestinationId, slot.DestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ItemId,
                    CircusPerformanceSupplyContracts.PerformancePropBoxItemId,
                    StringComparison.Ordinal)
                && value.Quantity > 0)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (props.Length != 1
            || CaptureMass(props[0]) !=
                CircusPerformanceSupplyContracts.PerformancePropBoxMassGrams)
        {
            status = "circus-performance-prop-box-exact-mass-invalid";
            return false;
        }

        DurableFacilityEquipmentUseResult used = use.TryApplyWearAndEffect(
            assignment.Key,
            CircusPerformanceSupplyPolicySource.BanquetCartRequirementId,
            BanquetCartWearPerShow,
            new PropSinkEffect(order, props[0], dispositions));
        if (!used.Succeeded)
        {
            status = used.FailureReason;
            return false;
        }
        return CircusShowSupplyOutbox.TryFinalize(
            order,
            items,
            dispositions,
            out status);
    }

    private long CaptureMass(WorldItemStackSnapshot stack)
    {
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            items.MassQuery,
            (ItemDefinitionId)stack.ItemId,
            stack.ItemInstanceId,
            stack.Components);
        return items.MassQuery.GetQuantityMass(
            (ItemDefinitionId)stack.ItemId,
            subject,
            1).Value;
    }

    private DurableFacilityEquipmentAssignment CreateAssignment(
        BuildingInstanceId stageId,
        Vector2Int position)
    {
        if (!stageId.IsValid)
            throw new ArgumentException("Circus supplies require a valid stage ID.");
        if (!policies.TryGetPolicy(
                CircusPerformanceSupplyPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The circus performance-supply policy is not registered.");
        }
        return policy.CreateAssignment(
            stageId.Value,
            stageId,
            position);
    }

    private static void ThrowOnConflict(
        DurableFacilityEquipmentSlotResult result,
        string operation)
    {
        if (result.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Circus performance-supply " + operation
                + " conflicted: " + result.FailureReason);
        }
    }

    private sealed class PropSinkEffect :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly CircusShowOrder order;
        private readonly WorldItemStackSnapshot prop;
        private readonly IPhysicalItemBatchDispositionService dispositions;

        internal PropSinkEffect(
            CircusShowOrder order,
            WorldItemStackSnapshot prop,
            IPhysicalItemBatchDispositionService dispositions)
        {
            this.order = order ?? throw new ArgumentNullException(nameof(order));
            this.prop = prop ?? throw new ArgumentNullException(nameof(prop));
            this.dispositions = dispositions
                ?? throw new ArgumentNullException(nameof(dispositions));
        }

        public string EffectKind => CircusPerformanceSupplyRuntime.EffectKind;

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
                    CircusPerformanceSupplyPolicySource.PolicyId,
                    StringComparison.Ordinal)
                && string.Equals(
                    requirement.RequirementId,
                    CircusPerformanceSupplyPolicySource.BanquetCartRequirementId,
                    StringComparison.Ordinal)
                && requirement.ItemId.Equals(
                    (ItemDefinitionId)DurableToolItemRules.BanquetCart)
                && Math.Abs(wearAmount - BanquetCartWearPerShow) <= 0.000001d
                && ReadDurability(subject) >= BanquetCartWearPerShow
                && prop.Quantity > 0
                && string.Equals(
                    prop.DestinationId,
                    slot.DestinationId,
                    StringComparison.Ordinal)
                && order.nextSupplyOperationSequence > 0;
            failureReason = valid
                ? string.Empty
                : "circus-performance-prop-sink-preflight-mismatch";
            return valid;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            int sequence = order.nextSupplyOperationSequence;
            string operationId = CircusShowSupplyOutbox.FormatOperationId(
                order.orderId,
                sequence);
            if (!dispositions.TryCommitPending(
                    new[] { new PhysicalItemTransformInput(prop.StackId, 1) },
                    PhysicalItemDispositionKind.Sink,
                    operationId,
                    CircusShowSupplyOutbox.ReasonCode,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out failureReason))
            {
                return false;
            }
            if (receipt.InputMassGrams !=
                CircusPerformanceSupplyContracts.PerformancePropBoxMassGrams)
            {
                throw new InvalidOperationException(
                    "Circus prop Sink receipt violated the exact 1,950g contract.");
            }
            CircusShowSupplyOutbox.Record(
                order,
                sequence,
                receipt,
                context.After.StackId,
                (float)ReadDurability(context.Before),
                (float)ReadDurability(context.After));
            failureReason = string.Empty;
            return true;
        }

        private static double ReadDurability(
            DurableFacilityEquipmentUseSubject subject)
        {
            DurableFacilityEquipmentComponentSnapshot component =
                subject.Components.Single(value => string.Equals(
                    value.ComponentTypeId,
                    ItemInstanceComponentIds.Durability,
                    StringComparison.Ordinal));
            return component.Values.Single(value =>
                    string.Equals(value.Key, "current", StringComparison.Ordinal)
                    && value.Kind == ItemStateValueKind.Decimal)
                .DecimalValue;
        }
    }
}
