using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Editor-fixture composition for equipment-module Source publication. Runtime
/// composition always uses PhysicalItemExactSourcePublicationService. This
/// adapter keeps the broad legacy equipment scenario matrix focused on module
/// domain behavior without restoring a production direct-spawn bypass.
/// </summary>
internal sealed class EditorEquipmentModuleExactSourcePublicationProxy :
    IPhysicalItemExactSourcePublicationService
{
    private sealed class Pending
    {
        public PhysicalItemExactSourcePublicationPlan Plan;
        public string StackId;
        public ItemInstanceId InstanceId;
    }

    private readonly IEquipmentPhysicalItemGateway items;
    private readonly Dictionary<string, Pending> pending =
        new(StringComparer.Ordinal);

    internal EditorEquipmentModuleExactSourcePublicationProxy(
        IEquipmentPhysicalItemGateway items)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public bool TryPrepare(
        PhysicalItemExactSourcePublicationPlan plan,
        out PhysicalItemExactSourcePublicationTransaction transaction,
        out string failureReason)
    {
        transaction = default;
        failureReason = string.Empty;
        if (plan == null
            || plan.Outputs.Count != 1
            || pending.ContainsKey(plan.BatchCommitId))
        {
            failureReason = "editor-module-source-plan-invalid";
            return false;
        }

        FacilityBufferPlannedOutputSlice output = plan.Outputs[0];
        ItemInstanceId instanceId =
            (ItemInstanceId)output.Subject.ItemInstanceId;
        if (output.Quantity != 1
            || !string.Equals(
                output.UniqueBindingCapabilityId,
                EquipmentModulePreparedOutputCodec.CapabilityId,
                StringComparison.Ordinal)
            || !instanceId.IsValid)
        {
            failureReason = "editor-module-source-output-invalid";
            return false;
        }

        ItemInstanceComponentSaveData prepared = output
            .MaterializeEditorFixtureComponents()
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    EquipmentModulePreparedOutputCodec.ComponentTypeId,
                    StringComparison.Ordinal));
        if (prepared == null
            || !EquipmentModulePreparedOutputCodec.TryDecode(
                prepared,
                out EquipmentModuleInstance desired,
                out failureReason)
            || !items.SpawnExistingUniqueItemAt(
                output.ItemDefinitionId.Value,
                instanceId,
                plan.DropPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "editor-module-source-spawn-failed"
                : failureReason;
            return false;
        }

        desired.sourceStackId = stackId;
        if (!items.TrySetInstanceComponent(
                stackId,
                EquipmentModuleItemStateCodec.Encode(desired)))
        {
            items.TryAbsorbUniqueItemStack(stackId, instanceId);
            failureReason = "editor-module-source-component-bind-failed";
            return false;
        }

        pending.Add(plan.BatchCommitId, new Pending
        {
            Plan = plan,
            StackId = stackId,
            InstanceId = instanceId
        });
        transaction = PhysicalItemExactSourcePublicationTransaction
            .CreateEditorFixture(
                plan.BatchCommitId,
                plan.DestinationId,
                stackId,
                output.OutputLineId,
                output.ItemDefinitionId,
                1,
                new PhysicalMassGrams(1),
                instanceId.Value);
        return true;
    }

    public bool TryCommitRetained(
        PhysicalItemExactSourcePublicationTransaction transaction,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (!TryTake(transaction, out _, out failureReason))
            return false;
        return true;
    }

    public bool TryCommitReleased(
        PhysicalItemExactSourcePublicationTransaction transaction,
        Vector2Int releasePosition,
        string reasonCode,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!IsCanonical(reasonCode))
        {
            failureReason = "editor-module-source-release-reason-invalid";
            return false;
        }
        if (!TryTake(transaction, out _, out failureReason))
            return false;
        return true;
    }

    public bool TryCommitReleased(
        PhysicalItemExactSourcePublicationTransaction transaction,
        FacilityBufferAcknowledgedOutputReleaseTarget target,
        string reasonCode,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!IsCanonical(reasonCode) || !IsValidTarget(target))
        {
            failureReason = "editor-module-source-release-target-invalid";
            return false;
        }
        if (!TryResolve(
                transaction,
                out Pending state,
                out failureReason))
            return false;
        if (target.HasDestination && !TryRoute(
                state.StackId,
                target,
                out failureReason))
            return false;
        pending.Remove(transaction.BatchCommitId);
        return true;
    }

    public bool TryRollback(
        PhysicalItemExactSourcePublicationTransaction transaction,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCanonical(reasonCode)
            || !TryResolve(transaction, out Pending state, out failureReason))
            return false;
        if (!items.TryAbsorbUniqueItemStack(state.StackId, state.InstanceId))
        {
            failureReason = "editor-module-source-rollback-absorb-failed";
            return false;
        }
        pending.Remove(transaction.BatchCommitId);
        return true;
    }

    public bool TryReleaseRetained(
        PhysicalItemExactSourcePublicationPlan plan,
        Vector2Int releasePosition,
        string reasonCode,
        out int releasedQuantity,
        out string failureReason)
    {
        releasedQuantity = 0;
        failureReason = "editor-module-retained-release-unsupported";
        return false;
    }

    public bool TrySinkRetained(
        PhysicalItemExactSourcePublicationPlan plan,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt disposition,
        out string failureReason)
    {
        disposition = default;
        failureReason = "editor-module-retained-sink-unsupported";
        return false;
    }

    private bool TryRoute(
        string stackId,
        FacilityBufferAcknowledgedOutputReleaseTarget target,
        out string failureReason)
    {
        if (items is IWorldItemStackRuntime runtime)
        {
            return runtime.TryRouteStackToDestination(
                stackId,
                WorldItemStackState.FacilityBuffer,
                target.DestinationId,
                target.DestinationPosition,
                out failureReason);
        }
        if (items is EditorEquipmentPhysicalItemGatewayProxy proxy)
        {
            return proxy.TryRouteStackToDestinationForEditor(
                stackId,
                WorldItemStackState.FacilityBuffer,
                target.DestinationId,
                target.DestinationPosition,
                out failureReason);
        }
        failureReason = "editor-module-source-route-unavailable";
        return false;
    }

    private bool TryTake(
        PhysicalItemExactSourcePublicationTransaction transaction,
        out Pending state,
        out string failureReason)
    {
        if (!TryResolve(transaction, out state, out failureReason))
            return false;
        pending.Remove(transaction.BatchCommitId);
        return true;
    }

    private bool TryResolve(
        PhysicalItemExactSourcePublicationTransaction transaction,
        out Pending state,
        out string failureReason)
    {
        if (!transaction.IsPrepared
            || !pending.TryGetValue(transaction.BatchCommitId, out state)
            || !string.Equals(
                transaction.DestinationId,
                state.Plan.DestinationId,
                StringComparison.Ordinal))
        {
            state = null;
            failureReason = "editor-module-source-transaction-missing";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsValidTarget(
        FacilityBufferAcknowledgedOutputReleaseTarget target) =>
        !target.HasDestination
            ? string.IsNullOrEmpty(target.DestinationId)
            : IsCanonical(target.DestinationId);
}
