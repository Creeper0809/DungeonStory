using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns manual-water item custody, staged fluid application and acknowledgement.
/// All pending rows and reserves belong to the shared current network state.
/// </summary>
internal sealed class ManualWaterTransferRuntime
{
    private const string BottledCleanWaterItemId =
        FluidFacilityInputOwnerProjectionAuthority.CleanWaterItemId;
    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly FluidNetworkStateStore stateStore;
    private readonly FluidNetworkProjectionAdapter projectionAdapter;
    private readonly IFluidFacilityInputOwnerAuthority inputOwners;
    private readonly IInfrastructureFactJournal commandOutcomes;

    internal ManualWaterTransferRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService physicalDispositions,
        FluidNetworkStateStore stateStore,
        FluidNetworkProjectionAdapter projectionAdapter,
        IFluidFacilityInputOwnerAuthority inputOwners,
        IInfrastructureFactJournal commandOutcomes)
    {
        this.topologyRuntime = topologyRuntime;
        this.items = items;
        this.physicalDispositions = physicalDispositions;
        this.stateStore = stateStore;
        this.projectionAdapter = projectionAdapter;
        this.inputOwners = inputOwners;
        this.commandOutcomes = commandOutcomes;
    }

    internal bool TryConsumeManualContainer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        out DomainFailure failure)
    {
        commandOutcomes?.AssertIdle();
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        projectionAdapter.EnsureTopology(inputOwners);
        if (consumer == null
            || string.IsNullOrWhiteSpace(destinationId)
            || !string.Equals(
                destinationId,
                destinationId.Trim(),
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable);
            return false;
        }

        string destination = destinationId;
        if (!TryResolveManualWaterState(
                consumer,
                out string nodeId,
                out FluidNodeState state))
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable);
            return false;
        }

        if (!inputOwners.TryEnsureManualDestination(
                consumer,
                destinationId,
                amount,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Manual-water destination authority is unavailable: "
                + ownerFailure);
        }

        ManualWaterTransferState pending = state.PendingManualWaterTransfers
            .SingleOrDefault(value => value.ImmediateConsumption);
        if (pending == null)
        {
            int sequence = state.NextImmediateManualWaterOperationSequence;
            string operation =
                FluidPhysicalOperationIdentity
                    .FormatImmediateManualWaterOperationId(nodeId, sequence);
            if (!TryStageManualWaterTransferCore(
                    consumer,
                    destination,
                    amount,
                    operation,
                    sequence,
                    immediateConsumption: true,
                    out _,
                    out failure))
            {
                return false;
            }
            pending = state.PendingManualWaterTransfers.Single(value =>
                string.Equals(value.OperationId, operation, StringComparison.Ordinal));
        }
        else if (pending.OperationSequence
                    != state.NextImmediateManualWaterOperationSequence
                 || !string.Equals(
                     pending.OperationId,
                     FluidPhysicalOperationIdentity
                         .FormatImmediateManualWaterOperationId(
                             nodeId,
                             pending.OperationSequence),
                     StringComparison.Ordinal)
                 || !string.Equals(
                     pending.DestinationId,
                     destination,
                     StringComparison.Ordinal)
                 || !Mathf.Approximately(pending.RequestedWaterUnits, amount))
        {
            failure = new DomainFailure(
                FailureCode.IndustrialCommandInvalid,
                pending.OperationId,
                "manual-water-immediate-operation-conflict");
            return false;
        }

        if (!TryApplyStagedManualWaterTransfer(
                consumer,
                pending.OperationId,
                out _,
                out failure))
        {
            return false;
        }
        return AcknowledgeManualWaterTransfer(
            pending.OperationId,
            out failure);
    }

    internal bool CanConsumeManualContainer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        out DomainFailure failure)
    {
        commandOutcomes?.AssertIdle();
        failure = DomainFailure.None;
        string destination = destinationId ?? string.Empty;
        if (consumer == null
            || consumer.IsBuildingDestroyed
            || consumer.IsGridDestroyed
            || !consumer.PersistentInstanceId.IsValid
            || float.IsNaN(amount)
            || float.IsInfinity(amount)
            || !Canonical(destination))
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable);
            return false;
        }

        string nodeId = consumer.PersistentInstanceId.Value;
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.NodeIdsByBuilding.TryGetValue(
                consumer,
                out string registeredNodeId)
            || !string.Equals(
                registeredNodeId,
                nodeId,
                StringComparison.Ordinal)
            || !topology.Nodes.TryGetValue(
                nodeId,
                out IndustrialNodeDescriptor node)
            || !ReferenceEquals(node.Building, consumer)
            || (node.Channels & UtilityChannel.CleanWater) == 0
            || !stateStore.CurrentNodes.TryGetValue(
                nodeId,
                out FluidNodeState state))
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable);
            return false;
        }

        if (amount <= 0f)
        {
            return true;
        }

        ManualWaterTransferState pending = state.PendingManualWaterTransfers
            .SingleOrDefault(value => value.ImmediateConsumption);
        if (pending != null)
        {
            if (pending.OperationSequence
                    != state.NextImmediateManualWaterOperationSequence
                || !string.Equals(
                    pending.OperationId,
                    FluidPhysicalOperationIdentity
                        .FormatImmediateManualWaterOperationId(
                            nodeId,
                            pending.OperationSequence),
                    StringComparison.Ordinal)
                || !string.Equals(
                    pending.DestinationId,
                    destination,
                    StringComparison.Ordinal)
                || !Mathf.Approximately(
                    pending.RequestedWaterUnits,
                    amount))
            {
                failure = new DomainFailure(
                    FailureCode.IndustrialCommandInvalid,
                    pending.OperationId,
                    "manual-water-immediate-operation-conflict");
                return false;
            }

            if (pending.FluidStateApplied
                || state.ManualWaterReserve
                    + pending.TransferredWaterUnits
                    + 0.0001f
                    >= pending.RequestedWaterUnits)
            {
                return true;
            }

            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable,
                destination);
            return false;
        }

        int requiredWaterUnits = GetRequiredManualWaterUnits(
            amount,
            state.ManualWaterReserve);
        if (TrySelectManualWaterInputs(
                destination,
                requiredWaterUnits,
                out _))
        {
            return true;
        }

        failure = new DomainFailure(
            FailureCode.FluidManualWaterUnavailable,
            destination);
        return false;
    }

    internal bool TryStageManualWaterTransfer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        string operationId,
        out ManualWaterTransferReceipt receipt,
        out DomainFailure failure) =>
        TryStageManualWaterTransferCore(
            consumer,
            destinationId,
            amount,
            operationId,
            operationSequence: 0,
            immediateConsumption: false,
            out receipt,
            out failure);

    private bool TryStageManualWaterTransferCore(
        BuildableObject consumer,
        string destinationId,
        float amount,
        string operationId,
        int operationSequence,
        bool immediateConsumption,
        out ManualWaterTransferReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        string destination = destinationId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        if (consumer == null
            || amount < 0f
            || float.IsNaN(amount)
            || float.IsInfinity(amount)
            || destination.Length == 0
            || operation.Length == 0
            || (immediateConsumption
                ? operationSequence <= 0
                : operationSequence != 0)
            || !string.Equals(destination, destination.Trim(), StringComparison.Ordinal)
            || !string.Equals(operation, operation.Trim(), StringComparison.Ordinal))
        {
            failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
            return false;
        }

        projectionAdapter.EnsureTopology(inputOwners);
        if (!inputOwners.TryEnsureManualDestination(
                consumer,
                destination,
                amount,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Staged manual-water destination authority is unavailable: "
                + ownerFailure);
        }
        if (!TryResolveManualWaterState(consumer, out FluidNodeState state))
        {
            failure = new DomainFailure(FailureCode.FluidManualWaterUnavailable);
            return false;
        }

        ManualWaterTransferState operationOwner = stateStore.Nodes.Values
            .SelectMany(candidate => candidate.PendingManualWaterTransfers)
            .SingleOrDefault(value => string.Equals(
                value.OperationId,
                operation,
                StringComparison.Ordinal));
        if (operationOwner != null
            && !state.PendingManualWaterTransfers.Contains(operationOwner))
        {
            failure = new DomainFailure(
                FailureCode.IndustrialCommandInvalid,
                operation,
                "manual-water-operation-owner-conflict");
            return false;
        }

        ManualWaterTransferState existing = state.PendingManualWaterTransfers
            .SingleOrDefault(value => string.Equals(
                value.OperationId,
                operation,
                StringComparison.Ordinal));
        if (existing != null)
        {
            if (!string.Equals(existing.DestinationId, destination, StringComparison.Ordinal)
                || !Mathf.Approximately(existing.RequestedWaterUnits, amount)
                || existing.OperationSequence != operationSequence
                || existing.ImmediateConsumption != immediateConsumption)
            {
                failure = new DomainFailure(
                    FailureCode.IndustrialCommandInvalid,
                    operation,
                    "manual-water-operation-conflict");
                return false;
            }
            receipt = new ManualWaterTransferReceipt(existing);
            return receipt.IsValid;
        }

        int requiredWaterUnits = GetRequiredManualWaterUnits(
            amount,
            state.ManualWaterReserve);
        var pending = new ManualWaterTransferState
        {
            OperationId = operation,
            DestinationId = destination,
            OperationSequence = operationSequence,
            ImmediateConsumption = immediateConsumption,
            RequestedWaterUnits = amount,
            TransferredWaterUnits = requiredWaterUnits
        };
        if (requiredWaterUnits > 0)
        {
            if (!TrySelectManualWaterInputs(
                    destination,
                    requiredWaterUnits,
                    out List<PhysicalItemTransformInput> inputs)
                || !physicalDispositions.TryCommitPending(
                    inputs,
                    PhysicalItemDispositionKind.Transfer,
                    operation,
                    FluidPhysicalOperationIdentity.ManualReserveReasonCode,
                    out PhysicalItemBatchDispositionReceipt physicalReceipt,
                    out _))
            {
                int routed = items.GetAllStacks()
                    .Where(stack => stack != null
                        && string.Equals(stack.ItemId, BottledCleanWaterItemId, StringComparison.Ordinal)
                        && string.Equals(stack.DestinationId, destination, StringComparison.Ordinal))
                    .Sum(stack => Mathf.Max(0, stack.Quantity));
                int missing = Mathf.Max(0, requiredWaterUnits - routed);
                if (missing > 0)
                {
                    items.TryRequestItemDelivery(
                        BottledCleanWaterItemId,
                        missing,
                        consumer.centerPos,
                        destination,
                        out _,
                        out _);
                }
                failure = new DomainFailure(
                    FailureCode.FluidManualWaterUnavailable,
                    destination);
                return false;
            }

            pending.PhysicalCommitId = physicalReceipt.CommitId;
            pending.RequestFingerprint = physicalReceipt.RequestFingerprint;
            pending.InputMassGrams = physicalReceipt.InputMassGrams;
            pending.SourceStackIds.AddRange(physicalReceipt.SourceStackIds);
        }

        state.PendingManualWaterTransfers.Add(pending);
        stateStore.Touch();
        receipt = new ManualWaterTransferReceipt(pending);
        return receipt.IsValid;
    }

    internal bool TryApplyStagedManualWaterTransfer(
        BuildableObject consumer,
        string operationId,
        out ManualWaterTransferReceipt receipt,
        out DomainFailure failure)
    {
        commandOutcomes?.AssertIdle();
        receipt = default;
        failure = DomainFailure.None;
        string operation = operationId ?? string.Empty;
        if (consumer == null
            || operation.Length == 0
            || !TryResolveManualWaterState(consumer, out FluidNodeState state))
        {
            failure = new DomainFailure(FailureCode.FluidManualWaterUnavailable);
            return false;
        }
        ManualWaterTransferState pending = state.PendingManualWaterTransfers
            .SingleOrDefault(value => string.Equals(
                value.OperationId,
                operation,
                StringComparison.Ordinal));
        if (pending == null)
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable,
                operation,
                "manual-water-stage-missing");
            return false;
        }
        if (!pending.FluidStateApplied)
        {
            float available = state.ManualWaterReserve
                + pending.TransferredWaterUnits;
            if (available + 0.0001f < pending.RequestedWaterUnits)
            {
                failure = new DomainFailure(
                    FailureCode.FluidManualWaterUnavailable,
                    operation,
                    "manual-water-stage-underflow");
                return false;
            }
            state.ManualWaterReserve = Mathf.Max(
                0f,
                available - pending.RequestedWaterUnits);
            pending.FluidStateApplied = true;
            stateStore.Touch();
        }
        receipt = new ManualWaterTransferReceipt(pending);
        return receipt.IsValid;
    }

    internal bool AcknowledgeManualWaterTransfer(
        string operationId,
        out DomainFailure failure)
    {
        commandOutcomes?.AssertIdle();
        failure = DomainFailure.None;
        string operation = operationId ?? string.Empty;
        if (operation.Length == 0)
        {
            failure = new DomainFailure(FailureCode.FluidManualWaterUnavailable);
            return false;
        }
        KeyValuePair<string, FluidNodeState> owner = stateStore.Nodes
            .SingleOrDefault(candidate =>
                candidate.Value.PendingManualWaterTransfers.Any(value =>
                    string.Equals(
                        value.OperationId,
                        operation,
                        StringComparison.Ordinal)));
        FluidNodeState state = owner.Value;
        ManualWaterTransferState pending = state?.PendingManualWaterTransfers
            .SingleOrDefault(value => string.Equals(value.OperationId, operation, StringComparison.Ordinal));
        if (pending == null)
        {
            return true;
        }
        if (!pending.FluidStateApplied)
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable,
                operation,
                "manual-water-stage-not-applied");
            return false;
        }
        if (pending.PhysicalCommitId.Length > 0
            && !physicalDispositions.Acknowledge(
                pending.PhysicalCommitId,
                out string acknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable,
                operation,
                acknowledgeFailure);
            return false;
        }
        if (pending.ImmediateConsumption)
        {
            if (pending.OperationSequence
                    != state.NextImmediateManualWaterOperationSequence
                || !string.Equals(
                    pending.OperationId,
                    FluidPhysicalOperationIdentity
                        .FormatImmediateManualWaterOperationId(
                            owner.Key,
                            pending.OperationSequence),
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.IndustrialCommandInvalid,
                    operation,
                    "manual-water-immediate-sequence-conflict");
                return false;
            }
            state.NextImmediateManualWaterOperationSequence = checked(
                state.NextImmediateManualWaterOperationSequence + 1);
        }
        state.PendingManualWaterTransfers.Remove(pending);
        stateStore.Touch();
        return true;
    }

    private bool TryResolveManualWaterState(
        BuildableObject consumer,
        out FluidNodeState state)
    {
        return TryResolveManualWaterState(consumer, out _, out state);
    }

    private bool TryResolveManualWaterState(
        BuildableObject consumer,
        out string nodeId,
        out FluidNodeState state)
    {
        nodeId = IndustrialInfrastructureIdentity.GetNodeId(consumer);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            state = null;
            return false;
        }
        if (projectionAdapter.TryResolveState(consumer, out state))
        {
            return true;
        }
        state = stateStore.EnsureState(nodeId);
        return true;
    }

    private static int GetRequiredManualWaterUnits(
        float amount,
        float manualWaterReserve) =>
        Mathf.Max(
            0,
            Mathf.CeilToInt(amount - manualWaterReserve - 0.0001f));

    private bool TrySelectManualWaterInputs(
        string destination,
        int requiredWaterUnits,
        out List<PhysicalItemTransformInput> inputs)
    {
        int remaining = requiredWaterUnits;
        inputs = new List<PhysicalItemTransformInput>();
        if (remaining == 0)
        {
            return true;
        }

        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(stack => stack != null
                         && stack.State == WorldItemStackState.FacilityBuffer
                         && !stack.HasReservations
                         && string.Equals(
                             stack.ItemId,
                             BottledCleanWaterItemId,
                             StringComparison.Ordinal)
                         && string.Equals(
                             stack.DestinationId,
                             destination,
                             StringComparison.Ordinal))
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            int quantity = Mathf.Min(remaining, stack.AvailableQuantity);
            if (quantity <= 0)
            {
                continue;
            }

            inputs.Add(new PhysicalItemTransformInput(stack.StackId, quantity));
            remaining -= quantity;
            if (remaining == 0)
            {
                break;
            }
        }

        return remaining == 0;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
