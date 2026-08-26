using System;
using System.Collections.Generic;

internal sealed class ElectricalNetworkAggregateState
{
    public readonly Dictionary<string, ElectricalNodeState> Nodes =
        new Dictionary<string, ElectricalNodeState>(StringComparer.Ordinal);
    public int Version;

    public ElectricalNetworkAggregateState DeepClone()
    {
        ElectricalNetworkAggregateState clone =
            new ElectricalNetworkAggregateState { Version = Version };
        foreach (KeyValuePair<string, ElectricalNodeState> pair in Nodes)
        {
            ElectricalNodeState source = pair.Value;
            clone.Nodes.Add(pair.Key, new ElectricalNodeState
            {
                Priority = source.Priority,
                StoredPower = source.StoredPower,
                FuelSeconds = source.FuelSeconds,
                Heat = source.Heat,
                Fault = source.Fault,
                BreakerTripped = source.BreakerTripped,
                Powered = source.Powered,
                SuppliedFraction = source.SuppliedFraction,
                NextFuelOperationSequence = source.NextFuelOperationSequence,
                PendingFuel = source.PendingFuel?.Clone()
                    ?? new PowerFuelCommitSaveData()
            });
        }

        return clone;
    }
}

public sealed class ElectricalNetworkRestoreCandidate
{
    internal ElectricalNetworkRestoreCandidate(ElectricalNetworkAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal ElectricalNetworkAggregateState State { get; }
}

internal sealed class FluidNetworkAggregateState
{
    public readonly Dictionary<string, FluidNodeState> Nodes =
        new Dictionary<string, FluidNodeState>(StringComparer.Ordinal);
    public int Version;

    public FluidNetworkAggregateState DeepClone()
    {
        FluidNetworkAggregateState clone =
            new FluidNetworkAggregateState { Version = Version };
        foreach (KeyValuePair<string, FluidNodeState> pair in Nodes)
        {
            FluidNodeState source = pair.Value;
            FluidNodeState clonedNode = new FluidNodeState
            {
                CleanWater = source.CleanWater,
                UnsafeWater = source.UnsafeWater,
                FoulWater = source.FoulWater,
                Wastewater = source.Wastewater,
                Blockage = source.Blockage,
                Leak = source.Leak,
                ProcessorWork = source.ProcessorWork,
                ManualWaterReserve = source.ManualWaterReserve,
                NextImmediateManualWaterOperationSequence =
                    source.NextImmediateManualWaterOperationSequence,
                NextContainerFeedOperationSequence =
                    source.NextContainerFeedOperationSequence,
                PendingContainerFeed = source.PendingContainerFeed?.DeepClone()
                    ?? new ContainerWaterFeedState(),
                TransferMode = source.TransferMode,
                TransferWork = source.TransferWork,
                TransferStatus = source.TransferStatus
            };
            clonedNode.PendingManualWaterTransfers.AddRange(
                source.PendingManualWaterTransfers.ConvertAll(value => value.DeepClone()));
            clone.Nodes.Add(pair.Key, clonedNode);
        }

        return clone;
    }
}

public sealed class FluidNetworkRestoreCandidate
{
    internal FluidNetworkRestoreCandidate(FluidNetworkAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal FluidNetworkAggregateState State { get; }
}

internal sealed class ConveyorAggregateState
{
    public readonly Dictionary<string, ConveyorNodeRuntimeState> Nodes =
        new Dictionary<string, ConveyorNodeRuntimeState>(StringComparer.Ordinal);
    public readonly Dictionary<string, ConveyorPayloadRuntimeState> Payloads =
        new Dictionary<string, ConveyorPayloadRuntimeState>(StringComparer.Ordinal);
    public int NextPayloadSequence = 1;
    public int Version;

    public ConveyorAggregateState DeepClone()
    {
        ConveyorAggregateState clone = new ConveyorAggregateState
        {
            NextPayloadSequence = NextPayloadSequence,
            Version = Version
        };
        foreach (KeyValuePair<string, ConveyorNodeRuntimeState> pair in Nodes)
        {
            clone.Nodes.Add(pair.Key, pair.Value.DeepClone());
        }

        foreach (KeyValuePair<string, ConveyorPayloadRuntimeState> pair in Payloads)
        {
            clone.Payloads.Add(pair.Key, pair.Value.DeepClone());
        }

        return clone;
    }
}
