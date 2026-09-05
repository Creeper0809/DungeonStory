using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

internal sealed class FluidNetworkRuntime :
    IFluidInfrastructureQuery,
    IFluidInfrastructureTransaction,
    IManualWaterTransferTransaction,
    IFluidInfrastructureBatchTransaction,
    IFluidWastewaterTransaction,
    IFluidInfrastructureCommand,
    IFluidInfrastructurePersistence,
    IFluidFacilityInputOwnerAuthority,
    ITickable
{
    private const float TickInterval = 0.5f;
    private const float BackflowWarningInterval = 5f;
    private const string BottledCleanWaterItemId = "resource:clean-water";

    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IPowerInfrastructureQuery power;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly IWorldFilthQuery filth;
    private readonly IGameClock clock;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IBuildingFacilityStateChangePort facilityStateChanges;
    private readonly FluidNetworkStateStore stateStore;
    private readonly FluidNetworkProjectionAdapter projectionAdapter;
    private readonly IFluidFacilityInputOwnerAuthority inputOwners;
    private readonly Dictionary<string, float> nextBackflowAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private IReadOnlyList<WaterTransferFacilitySnapshot> waterTransfers =
        Array.Empty<WaterTransferFacilitySnapshot>();
    private float accumulated;

    [Inject]
    public FluidNetworkRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IPowerInfrastructureQuery power,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IWorldFilthQuery filth,
        IGameClock clock,
        IFacilityCapabilityQuery facilities,
        IBuildingFacilityStateChangePort facilityStateChanges,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IPhysicalItemMassQuery physicalMass,
        IFacilityBufferDestinationClaimAuthorityQuery destinationClaims,
        IFacilityBufferMassCapacityAuthorityQuery destinationCapacities,
        IFacilityBufferDestinationLifecycleCommand destinationLifecycle,
        IFacilityBufferDestinationReleaseService destinationReleases)
        : this(
            topologyRuntime,
            power,
            items,
            physicalDispositions,
            filth,
            clock,
            facilities,
            facilityStateChanges,
            aggregateRootStore,
            new FluidFacilityInputOwnerAuthority(
                physicalMass,
                destinationClaims,
                destinationCapacities,
                destinationLifecycle,
                destinationReleases))
    {
    }

    internal FluidNetworkRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IPowerInfrastructureQuery power,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IWorldFilthQuery filth,
        IGameClock clock,
        IFacilityCapabilityQuery facilities,
        IBuildingFacilityStateChangePort facilityStateChanges,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IFluidFacilityInputOwnerAuthority inputOwners)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.physicalDispositions = physicalDispositions
            ?? throw new ArgumentNullException(nameof(physicalDispositions));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.facilityStateChanges = facilityStateChanges
            ?? throw new ArgumentNullException(nameof(facilityStateChanges));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        stateStore = new FluidNetworkStateStore(
            aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
        projectionAdapter = new FluidNetworkProjectionAdapter(
            this.topologyRuntime,
            stateStore);
    }

#if UNITY_EDITOR
    internal FluidNetworkRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IPowerInfrastructureQuery power,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IWorldFilthQuery filth,
        IGameClock clock,
        IFacilityCapabilityQuery facilities,
        IBuildingFacilityStateChangePort facilityStateChanges,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
        : this(
            topologyRuntime,
            power,
            items,
            physicalDispositions,
            filth,
            clock,
            facilities,
            facilityStateChanges,
            aggregateRootStore,
            new EditorFluidFacilityInputOwnerAuthority())
    {
    }
#endif

    public int Version => stateStore.Version;

    bool IFluidFacilityInputOwnerAuthority.TryReconcile(
        IndustrialTopologySnapshot topology,
        out string failureReason) => inputOwners.TryReconcile(
        topology,
        out failureReason);

    bool IFluidFacilityInputOwnerAuthority.TryEnsureManualDestination(
        BuildableObject facility,
        string destinationId,
        float requestedWaterUnits,
        out string failureReason)
    {
        EnsureTopology();
        return inputOwners.TryEnsureManualDestination(
            facility,
            destinationId,
            requestedWaterUnits,
            out failureReason);
    }

    public IReadOnlyList<FluidNetworkSnapshot> Networks
    {
        get
        {
            EnsureTopology();
            return projectionAdapter.GetSnapshots(Version);
        }
    }

    public IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers
    {
        get
        {
            EnsureTopology();
            RefreshWaterTransferSnapshots();
            return waterTransfers;
        }
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        EnsureTopology();
        accumulated += clock.DeltaTime;
        if (accumulated < TickInterval)
        {
            return;
        }

        float deltaTime = accumulated;
        accumulated = 0f;
        ProduceWater(deltaTime);
        TransferContainerWater(deltaTime);
        ProcessWastewater(deltaTime);
        ApplyLeaksAndBackflow(deltaTime);
        stateStore.Touch();
    }

    public bool TryConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out WorldWaterQuality consumedQuality,
        out DomainFailure failure)
    {
        consumedQuality = WorldWaterQuality.Foul;
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            consumedQuality = WorldWaterQuality.Clean;
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                consumer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            failure = new DomainFailure(FailureCode.FluidNetworkUnavailable);
            return false;
        }

        WorldWaterQuality[] order =
            FluidNodeWaterRules.GetConsumptionOrder(minimumQuality);
        foreach (WorldWaterQuality quality in order)
        {
            float available = GetNetworkWater(networkId, quality);
            if (available + 0.0001f < amount)
            {
                continue;
            }

            RemoveNetworkWater(networkId, quality, amount);
            consumedQuality = quality;
            stateStore.Touch();
            return true;
        }

        failure = new DomainFailure(
            FailureCode.FluidInsufficientWater,
            minimumQuality.ToString(),
            amount.ToString("0.###"));
        return false;
    }

    public bool CanConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                consumer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            failure = new DomainFailure(FailureCode.FluidNetworkUnavailable);
            return false;
        }

        foreach (WorldWaterQuality quality in
                 FluidNodeWaterRules.GetConsumptionOrder(minimumQuality))
        {
            if (GetNetworkWater(networkId, quality) + 0.0001f >= amount)
            {
                return true;
            }
        }

        failure = new DomainFailure(
            FailureCode.FluidInsufficientWater,
            minimumQuality.ToString(),
            amount.ToString("0.###"));
        return false;
    }

    public bool TryCommitBatch(
        IReadOnlyList<FluidNetworkBatchDemand> demands,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (demands == null)
        {
            failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
            return false;
        }

        EnsureTopology();
        var ordered = demands
            .Select((demand, index) => (Demand: demand, Index: index))
            .OrderBy(value => (int)value.Demand.MinimumQuality)
            .ThenBy(value => IndustrialInfrastructureIdentity.GetNodeId(
                value.Demand.Consumer), StringComparer.Ordinal)
            .ThenBy(value => value.Index)
            .ToArray();
        Dictionary<(string NetworkId, WorldWaterQuality Quality), float>
            simulatedWater = new();
        Dictionary<string, float> simulatedWastewater =
            new(StringComparer.Ordinal);
        List<(string NetworkId, WorldWaterQuality Quality, float Amount)>
            waterCommits = new();
        Dictionary<string, float> wastewaterCommits =
            new(StringComparer.Ordinal);

        foreach ((FluidNetworkBatchDemand demand, _) in ordered)
        {
            if (demand.Consumer == null
                || !Enum.IsDefined(typeof(WorldWaterQuality), demand.MinimumQuality)
                || float.IsNaN(demand.CleanWater)
                || float.IsInfinity(demand.CleanWater)
                || demand.CleanWater < 0f
                || float.IsNaN(demand.Wastewater)
                || float.IsInfinity(demand.Wastewater)
                || demand.Wastewater < 0f)
            {
                failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
                return false;
            }

            if (demand.CleanWater > 0f)
            {
                if (!projectionAdapter.TryResolveNetwork(
                        demand.Consumer,
                        UtilityChannel.CleanWater,
                        out string waterNetworkId,
                        out _))
                {
                    failure = new DomainFailure(FailureCode.FluidNetworkUnavailable);
                    return false;
                }

                bool allocated = false;
                foreach (WorldWaterQuality quality in
                         FluidNodeWaterRules.GetConsumptionOrder(
                             demand.MinimumQuality))
                {
                    var key = (waterNetworkId, quality);
                    if (!simulatedWater.TryGetValue(key, out float available))
                    {
                        available = GetNetworkWater(waterNetworkId, quality);
                    }
                    if (available + 0.0001f < demand.CleanWater)
                    {
                        continue;
                    }
                    simulatedWater[key] = available - demand.CleanWater;
                    waterCommits.Add((
                        waterNetworkId,
                        quality,
                        demand.CleanWater));
                    allocated = true;
                    break;
                }
                if (!allocated)
                {
                    failure = new DomainFailure(
                        FailureCode.FluidInsufficientWater,
                        demand.MinimumQuality.ToString(),
                        demand.CleanWater.ToString("0.###"));
                    return false;
                }
            }

            if (demand.Wastewater > 0f)
            {
                if (!projectionAdapter.TryResolveNetwork(
                        demand.Consumer,
                        UtilityChannel.Wastewater,
                        out string wasteNetworkId,
                        out _))
                {
                    failure = new DomainFailure(
                        FailureCode.FluidWastewaterUnavailable);
                    return false;
                }
                if (!simulatedWastewater.TryGetValue(
                        wasteNetworkId,
                        out float current))
                {
                    current = GetNetworkWastewater(wasteNetworkId);
                }
                float next = current + demand.Wastewater;
                if (next > GetWastewaterCapacity(wasteNetworkId) + 0.0001f)
                {
                    failure = new DomainFailure(
                        FailureCode.FluidWastewaterUnavailable,
                        demand.Wastewater.ToString("0.###"));
                    return false;
                }
                simulatedWastewater[wasteNetworkId] = next;
                wastewaterCommits[wasteNetworkId] =
                    wastewaterCommits.TryGetValue(wasteNetworkId, out float total)
                        ? total + demand.Wastewater
                        : demand.Wastewater;
            }
        }

        foreach ((string networkId, WorldWaterQuality quality, float amount)
                 in waterCommits)
        {
            RemoveNetworkWater(networkId, quality, amount);
        }
        foreach (KeyValuePair<string, float> commit in wastewaterCommits
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            AddNetworkWastewater(commit.Key, commit.Value);
        }
        if (waterCommits.Count > 0 || wastewaterCommits.Count > 0)
        {
            stateStore.Touch();
        }
        return true;
    }

    public bool TryAdd(
        BuildableObject producer,
        WorldWaterQuality quality,
        float amount,
        out float accepted)
    {
        accepted = 0f;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                producer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            return false;
        }

        accepted = AddNetworkWater(networkId, quality, amount);
        if (accepted > 0f)
        {
            stateStore.Touch();
        }

        return accepted + 0.0001f >= amount;
    }

    public bool TryConsumeManualContainer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        EnsureTopology();
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

    public bool TryStageManualWaterTransfer(
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

        EnsureTopology();
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

        int requiredWaterUnits = Mathf.Max(
            0,
            Mathf.CeilToInt(amount - state.ManualWaterReserve - 0.0001f));
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
            int remaining = requiredWaterUnits;
            var inputs = new List<PhysicalItemTransformInput>();
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

            if (remaining > 0
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

    public bool TryApplyStagedManualWaterTransfer(
        BuildableObject consumer,
        string operationId,
        out ManualWaterTransferReceipt receipt,
        out DomainFailure failure)
    {
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

    public bool AcknowledgeManualWaterTransfer(
        string operationId,
        out DomainFailure failure)
    {
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

    public bool TryGetNetwork(
        BuildableObject building,
        out FluidNetworkSnapshot snapshot)
    {
        EnsureTopology();
        snapshot = null;
        if (building == null)
        {
            return false;
        }

        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.NodeIdsByBuilding.TryGetValue(
                building,
                out string nodeId))
        {
            return false;
        }

        string cleanId = projectionAdapter.ResolveNetworkId(
            topology,
            UtilityChannel.CleanWater,
            nodeId);
        string wasteId = projectionAdapter.ResolveNetworkId(
            topology,
            UtilityChannel.Wastewater,
            nodeId);
        snapshot = projectionAdapter.GetSnapshots(Version).FirstOrDefault(candidate =>
            string.Equals(candidate.NetworkId, cleanId, StringComparison.Ordinal)
            || string.Equals(candidate.NetworkId, wasteId, StringComparison.Ordinal));
        return snapshot != null;
    }

    public bool TryAddWastewater(
        BuildableObject fixture,
        float amount,
        out float accepted,
        out DomainFailure failure)
    {
        accepted = 0f;
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                fixture,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            CreateBackflow(fixture, amount);
            failure = new DomainFailure(
                FailureCode.FluidWastewaterUnavailable);
            return false;
        }

        float capacity = GetWastewaterCapacity(networkId);
        float current = GetNetworkWastewater(networkId);
        accepted = Mathf.Min(amount, Mathf.Max(0f, capacity - current));
        AddNetworkWastewater(networkId, accepted);
        if (accepted + 0.0001f < amount)
        {
            CreateBackflow(fixture, amount - accepted);
            failure = new DomainFailure(
                FailureCode.FluidWastewaterUnavailable,
                amount.ToString("0.###"),
                accepted.ToString("0.###"));
        }

        stateStore.Touch();
        return accepted + 0.0001f >= amount;
    }

    public bool TryConsumeWastewater(
        BuildableObject processor,
        float amount,
        out float consumed)
    {
        consumed = 0f;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                processor,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            return false;
        }

        consumed = RemoveNetworkWastewater(networkId, amount);
        if (consumed > 0f)
        {
            stateStore.Touch();
        }

        return consumed + 0.0001f >= amount;
    }

    public bool CanAcceptWastewater(
        BuildableObject fixture,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                fixture,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.FluidWastewaterUnavailable);
            return false;
        }

        float free = Mathf.Max(
            0f,
            GetWastewaterCapacity(networkId)
            - GetNetworkWastewater(networkId));
        if (free + 0.0001f >= amount)
        {
            return true;
        }

        failure = new DomainFailure(
            FailureCode.FluidWastewaterUnavailable,
            amount.ToString("0.###"));
        return false;
    }

    public InfrastructureCommandResult ClearBlockage(
        BuildableObject building)
    {
        if (!projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.FluidMaintenanceUnavailable);
        }

        bool changed = state.Blockage > 0.0001f;
        state.Blockage = 0f;
        stateStore.Touch();
        if (changed)
            facilityStateChanges.MarkDynamicStateDirty();
        return InfrastructureCommandResult.Success();
    }

    public InfrastructureCommandResult SetWaterTransferMode(
        BuildableObject building,
        WaterContainerTransferMode mode)
    {
        if (!Enum.IsDefined(typeof(WaterContainerTransferMode), mode)
            || building?.BuildingData?.GetAbility<
                BuildingWaterContainerTransferAbility>() == null
            || !projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.IndustrialCommandInvalid);
        }

        state.TransferMode = mode;
        state.TransferWork = 0f;
        state.TransferStatus = InfrastructureStatus.None;
        stateStore.Touch();
        return InfrastructureCommandResult.Success();
    }

    public bool TryGetMaintenance(
        BuildableObject building,
        out float blockage,
        out float leak)
    {
        EnsureTopology();
        if (projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            blockage = Mathf.Clamp(state.Blockage, 0f, 100f);
            leak = Mathf.Clamp(state.Leak, 0f, 100f);
            return true;
        }

        blockage = 0f;
        leak = 0f;
        return false;
    }

    public InfrastructureCommandResult RepairLeak(BuildableObject building)
    {
        if (!projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.FluidMaintenanceUnavailable);
        }

        bool changed = state.Leak > 0.0001f;
        state.Leak = 0f;
        stateStore.Touch();
        if (changed)
            facilityStateChanges.MarkDynamicStateDirty();
        return InfrastructureCommandResult.Success();
    }

    public DungeonFluidInfrastructureSaveData Capture()
    {
        EnsureTopology();
        return new DungeonFluidInfrastructureSaveData
        {
            nodes = stateStore.Nodes
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new FluidNodeSaveData
                {
                    buildingInstanceId = pair.Key,
                    cleanWater = pair.Value.CleanWater,
                    unsafeWater = pair.Value.UnsafeWater,
                    foulWater = pair.Value.FoulWater,
                    wastewater = pair.Value.Wastewater,
                    blockage = pair.Value.Blockage,
                    leak = pair.Value.Leak,
                    processorWork = pair.Value.ProcessorWork,
                    manualWaterReserve = pair.Value.ManualWaterReserve,
                    nextImmediateManualWaterOperationSequence = pair.Value
                        .NextImmediateManualWaterOperationSequence,
                    pendingManualWaterTransfers = pair.Value
                        .PendingManualWaterTransfers
                        .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                        .Select(value => ToSaveData(value))
                        .ToList(),
                    nextContainerFeedOperationSequence = pair.Value
                        .NextContainerFeedOperationSequence,
                    pendingContainerFeed = ToSaveData(
                        pair.Value.PendingContainerFeed),
                    transferMode = pair.Value.TransferMode,
                    transferWork = pair.Value.TransferWork
                })
                .ToList()
        };
    }

    public FluidNetworkRestoreCandidate PrepareRestore(
        DungeonFluidInfrastructureSaveData snapshot)
    {
        IndustrialInfrastructureSaveValidation.RequireValid(snapshot);
        FluidNetworkAggregateState restored = new FluidNetworkAggregateState
        {
            Version = 1
        };
        foreach (FluidNodeSaveData saved in snapshot?.nodes
                 ?? new List<FluidNodeSaveData>())
        {
            if (saved == null
                || !new BuildingInstanceId(
                    saved.buildingInstanceId).IsValid)
            {
                continue;
            }

            FluidNodeState restoredNode = new FluidNodeState
            {
                CleanWater = Mathf.Max(0f, saved.cleanWater),
                UnsafeWater = Mathf.Max(0f, saved.unsafeWater),
                FoulWater = Mathf.Max(0f, saved.foulWater),
                Wastewater = Mathf.Max(0f, saved.wastewater),
                Blockage = Mathf.Clamp(saved.blockage, 0f, 100f),
                Leak = Mathf.Clamp(saved.leak, 0f, 100f),
                ProcessorWork = Mathf.Max(0f, saved.processorWork),
                ManualWaterReserve = Mathf.Max(
                    0f,
                    saved.manualWaterReserve),
                NextImmediateManualWaterOperationSequence =
                    saved.nextImmediateManualWaterOperationSequence,
                NextContainerFeedOperationSequence =
                    saved.nextContainerFeedOperationSequence,
                PendingContainerFeed = ToRuntimeState(
                    saved.pendingContainerFeed),
                TransferMode = Enum.IsDefined(
                    typeof(WaterContainerTransferMode),
                    saved.transferMode)
                        ? saved.transferMode
                        : WaterContainerTransferMode.Disabled,
                TransferWork = Mathf.Max(0f, saved.transferWork)
            };
            restoredNode.PendingManualWaterTransfers.AddRange(
                (saved.pendingManualWaterTransfers
                    ?? new List<ManualWaterTransferSaveData>())
                .Select(ToRuntimeState));
            restored.Nodes[saved.buildingInstanceId.Trim()] = restoredNode;
        }

        return new FluidNetworkRestoreCandidate(restored);
    }

    public void Restore(FluidNetworkRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        stateStore.Replace(candidate);
        if (!inputOwners.TryReconcile(
                topologyRuntime.Current,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Could not stage fluid input destination authorities: "
                + ownerFailure);
        }
        if (!stateStore.IsRestoreStaging)
        {
            ResetProjectionAfterRestore();
            EnsureTopology();
        }
    }

    private static ManualWaterTransferSaveData ToSaveData(
        ManualWaterTransferState state) => new ManualWaterTransferSaveData
    {
        operationId = state.OperationId,
        physicalCommitId = state.PhysicalCommitId,
        requestFingerprint = state.RequestFingerprint,
        destinationId = state.DestinationId,
        operationSequence = state.OperationSequence,
        immediateConsumption = state.ImmediateConsumption,
        requestedWaterUnits = state.RequestedWaterUnits,
        transferredWaterUnits = state.TransferredWaterUnits,
        inputMassGrams = state.InputMassGrams,
        fluidStateApplied = state.FluidStateApplied,
        sourceStackIds = new List<string>(state.SourceStackIds)
    };

    private static ManualWaterTransferState ToRuntimeState(
        ManualWaterTransferSaveData saved)
    {
        var state = new ManualWaterTransferState
        {
            OperationId = saved.operationId,
            PhysicalCommitId = saved.physicalCommitId,
            RequestFingerprint = saved.requestFingerprint,
            DestinationId = saved.destinationId,
            OperationSequence = saved.operationSequence,
            ImmediateConsumption = saved.immediateConsumption,
            RequestedWaterUnits = saved.requestedWaterUnits,
            TransferredWaterUnits = saved.transferredWaterUnits,
            InputMassGrams = saved.inputMassGrams,
            FluidStateApplied = saved.fluidStateApplied
        };
        state.SourceStackIds.AddRange(saved.sourceStackIds);
        return state;
    }

    private static ContainerWaterFeedCommitSaveData ToSaveData(
        ContainerWaterFeedState state) =>
        state == null
            ? new ContainerWaterFeedCommitSaveData()
            : new ContainerWaterFeedCommitSaveData
            {
                phase = state.Phase,
                operationSequence = state.OperationSequence,
                operationId = state.OperationId,
                reasonCode = state.ReasonCode,
                requestFingerprint = state.RequestFingerprint,
                physicalCommitId = state.PhysicalCommitId,
                nodeId = state.NodeId,
                networkId = state.NetworkId,
                destinationId = state.DestinationId,
                itemId = state.ItemId,
                quantity = state.Quantity,
                waterAmount = state.WaterAmount,
                inputMassGrams = state.InputMassGrams,
                sourceStackIds = new List<string>(state.SourceStackIds)
            };

    private static ContainerWaterFeedState ToRuntimeState(
        ContainerWaterFeedCommitSaveData saved)
    {
        if (saved == null)
        {
            return new ContainerWaterFeedState();
        }
        var state = new ContainerWaterFeedState
        {
            Phase = saved.phase,
            OperationSequence = saved.operationSequence,
            OperationId = saved.operationId,
            ReasonCode = saved.reasonCode,
            RequestFingerprint = saved.requestFingerprint,
            PhysicalCommitId = saved.physicalCommitId,
            NodeId = saved.nodeId,
            NetworkId = saved.networkId,
            DestinationId = saved.destinationId,
            ItemId = saved.itemId,
            Quantity = saved.quantity,
            WaterAmount = saved.waterAmount,
            InputMassGrams = saved.inputMassGrams
        };
        state.SourceStackIds.AddRange(
            saved.sourceStackIds ?? new List<string>());
        return state;
    }

    private void TransferContainerWater(float deltaTime)
    {
        foreach (IndustrialNodeDescriptor node in topologyRuntime.Current.Nodes
                     .Values.OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            BuildingWaterContainerTransferAbility transfer =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterContainerTransferAbility>();
            if (transfer == null)
            {
                continue;
            }

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            float required = Mathf.Max(0.1f, transfer.secondsPerBatch);
            if ((ContainerWaterFeedCommitPhase)(state.PendingContainerFeed?.Phase ?? 0)
                != ContainerWaterFeedCommitPhase.None)
            {
                bool recovered = TryRecoverContainerFeed(node, state, transfer);
                state.TransferWork = recovered
                    ? Mathf.Max(0f, state.TransferWork - required)
                    : Mathf.Min(state.TransferWork, required);
                continue;
            }
            if (state.TransferMode == WaterContainerTransferMode.Disabled)
            {
                state.TransferWork = 0f;
                state.TransferStatus = InfrastructureStatus.None;
                continue;
            }

            if (transfer.requiresPower && !power.IsPowered(node.Building))
            {
                state.TransferStatus = new InfrastructureStatus(
                    InfrastructureStatusCode.PowerUnavailable);
                continue;
            }

            state.TransferWork += deltaTime * ResolveFlowMultiplier(node);
            if (state.TransferWork + 0.0001f < required)
            {
                state.TransferStatus = InfrastructureStatus.None;
                continue;
            }

            bool completed = state.TransferMode
                == WaterContainerTransferMode.BottleFromNetwork
                    ? TryBottleWater(node, state, transfer)
                    : TryFeedWaterNetwork(node, state, transfer);
            if (completed)
            {
                state.TransferWork = Mathf.Max(
                    0f,
                    state.TransferWork - required);
            }
            else
            {
                state.TransferWork = Mathf.Min(state.TransferWork, required);
            }
        }

    }

    private bool TryBottleWater(
        IndustrialNodeDescriptor node,
        FluidNodeState state,
        BuildingWaterContainerTransferAbility transfer)
    {
        if (!items.CatalogProvider.TryGetDefinition(
                BottledCleanWaterItemId,
                out DungeonItemDefinition bottledWater)
            || bottledWater.StockCategory != StockCategory.Water)
        {
            throw new InvalidOperationException(
                $"Water-container transfer requires authored clean-water item '{BottledCleanWaterItemId}'.");
        }
        int currentStock = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    BottledCleanWaterItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        if (currentStock >= Mathf.Max(1, transfer.bottleTargetStock))
        {
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.OutputTargetReached,
                transfer.bottleTargetStock.ToString());
            return false;
        }

        if (!TryConsume(
                node.Building,
                WorldWaterQuality.Clean,
                transfer.waterPerBatch,
                out _,
                out DomainFailure failure))
        {
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.StorageCapacityUnavailable,
                failure.Code.ToString());
            return false;
        }

        if (!items.SpawnItemAt(
                BottledCleanWaterItemId,
                Mathf.Max(1, Mathf.RoundToInt(transfer.waterPerBatch)),
                node.Building.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned <= 0)
        {
            TryAdd(
                node.Building,
                WorldWaterQuality.Clean,
                transfer.waterPerBatch,
                out _);
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.OutputSpaceUnavailable);
            return false;
        }

        state.TransferStatus = InfrastructureStatus.None;
        return true;
    }

    private bool TryFeedWaterNetwork(
        IndustrialNodeDescriptor node,
        FluidNodeState state,
        BuildingWaterContainerTransferAbility transfer)
    {
        ContainerWaterFeedCommitPhase phase =
            (ContainerWaterFeedCommitPhase)(state.PendingContainerFeed?.Phase ?? 0);
        if (phase != ContainerWaterFeedCommitPhase.None)
        {
            return TryRecoverContainerFeed(node, state, transfer);
        }

        if (!projectionAdapter.TryResolveNetwork(
                node.Building,
                UtilityChannel.CleanWater,
                out string networkId,
                out _)
            || GetNetworkWaterFreeCapacity(networkId)
                + 0.0001f < transfer.waterPerBatch)
        {
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.StorageCapacityUnavailable);
            return false;
        }

        string destinationId = CreateWaterTransferDestinationId(node.NodeId);
        if (!inputOwners.TryEnsureManualDestination(
                node.Building,
                destinationId,
                transfer.waterPerBatch,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Container-water destination authority is unavailable: "
                + ownerFailure);
        }
        int quantity = Mathf.Max(
            1,
            Mathf.RoundToInt(transfer.waterPerBatch));
        int sequence = state.NextContainerFeedOperationSequence;
        string operationId =
            FluidPhysicalOperationIdentity.FormatContainerFeedOperationId(
                node.NodeId,
                sequence);
        state.PendingContainerFeed = new ContainerWaterFeedState
        {
            Phase = (int)ContainerWaterFeedCommitPhase.IntentRecorded,
            OperationSequence = sequence,
            OperationId = operationId,
            ReasonCode =
                FluidPhysicalOperationIdentity.ContainerFeedReasonCode,
            NodeId = node.NodeId,
            NetworkId = networkId,
            DestinationId = destinationId,
            ItemId = BottledCleanWaterItemId,
            Quantity = quantity,
            WaterAmount = transfer.waterPerBatch
        };
        stateStore.Touch();

        int remaining = quantity;
        var inputs = new List<PhysicalItemTransformInput>();
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
                             destinationId,
                             StringComparison.Ordinal))
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            int selected = Mathf.Min(remaining, stack.AvailableQuantity);
            if (selected <= 0)
            {
                continue;
            }
            inputs.Add(new PhysicalItemTransformInput(stack.StackId, selected));
            remaining -= selected;
            if (remaining == 0)
            {
                break;
            }
        }

        if (remaining > 0
            || !physicalDispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                FluidPhysicalOperationIdentity.ContainerFeedReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out _))
        {
            state.PendingContainerFeed =
                new ContainerWaterFeedState();
            stateStore.Touch();
            items.TryRequestItemDelivery(
                BottledCleanWaterItemId,
                quantity,
                node.Building.centerPos,
                destinationId,
                out _,
                out _);
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.InputDeliveryPending,
                destinationId);
            return false;
        }

        state.PendingContainerFeed.PhysicalCommitId = receipt.CommitId;
        state.PendingContainerFeed.RequestFingerprint =
            receipt.RequestFingerprint;
        state.PendingContainerFeed.InputMassGrams = receipt.InputMassGrams;
        state.PendingContainerFeed.SourceStackIds.AddRange(
            receipt.SourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal));
        stateStore.Touch();
        return TryRecoverContainerFeed(node, state, transfer);
    }

    private bool TryRecoverContainerFeed(
        IndustrialNodeDescriptor node,
        FluidNodeState state,
        BuildingWaterContainerTransferAbility transfer)
    {
        ContainerWaterFeedState pending =
            state.PendingContainerFeed
            ?? new ContainerWaterFeedState();
        ContainerWaterFeedCommitPhase phase =
            (ContainerWaterFeedCommitPhase)pending.Phase;
        if (phase == ContainerWaterFeedCommitPhase.None)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                node.Building,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            throw new InvalidOperationException(
                $"Container-water feed '{pending.OperationId}' lost its clean-water network.");
        }
        string destinationId = CreateWaterTransferDestinationId(node.NodeId);
        int expectedQuantity = Mathf.Max(
            1,
            Mathf.RoundToInt(transfer.waterPerBatch));
        bool contractMatches = pending.OperationSequence
                == state.NextContainerFeedOperationSequence
            && pending.Quantity == expectedQuantity
            && string.Equals(pending.NodeId, node.NodeId, StringComparison.Ordinal)
            && string.Equals(pending.NetworkId, networkId, StringComparison.Ordinal)
            && string.Equals(
                pending.DestinationId,
                destinationId,
                StringComparison.Ordinal)
            && string.Equals(
                pending.ItemId,
                BottledCleanWaterItemId,
                StringComparison.Ordinal)
            && string.Equals(
                pending.ReasonCode,
                FluidPhysicalOperationIdentity.ContainerFeedReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                pending.OperationId,
                FluidPhysicalOperationIdentity.FormatContainerFeedOperationId(
                    node.NodeId,
                    pending.OperationSequence),
                StringComparison.Ordinal)
            && Mathf.Approximately(
                pending.WaterAmount,
                transfer.waterPerBatch);
        if (!contractMatches)
        {
            throw new InvalidOperationException(
                $"Container-water feed '{pending.OperationId}' conflicts with node '{node.NodeId}'.");
        }
        if (!physicalDispositions.TryGetPending(
                pending.OperationId,
                out PhysicalItemBatchDispositionReceipt receipt)
            || !receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Transfer
            || receipt.Quantity != pending.Quantity
            || !string.Equals(
                receipt.OperationId,
                pending.OperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                pending.ReasonCode,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.RequestFingerprint,
                pending.RequestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.CommitId,
                pending.PhysicalCommitId,
                StringComparison.Ordinal)
            || receipt.InputMassGrams != pending.InputMassGrams
            || !receipt.SourceStackIds.SequenceEqual(
                pending.SourceStackIds,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Container-water feed '{pending.OperationId}' has no exact pending physical Transfer receipt.");
        }

        if (phase == ContainerWaterFeedCommitPhase.IntentRecorded)
        {
            if (GetNetworkWaterFreeCapacity(networkId) + 0.0001f
                < pending.WaterAmount)
            {
                state.TransferStatus = new InfrastructureStatus(
                    InfrastructureStatusCode.StorageCapacityUnavailable);
                return false;
            }
            float accepted = AddNetworkWater(
                networkId,
                WorldWaterQuality.Clean,
                pending.WaterAmount);
            if (accepted + 0.0001f < pending.WaterAmount)
            {
                throw new InvalidOperationException(
                    $"Container-water feed '{pending.OperationId}' could not publish its exact fluid outcome.");
            }
            pending.Phase =
                (int)ContainerWaterFeedCommitPhase.OutcomePublished;
            stateStore.Touch();
        }

        if (!physicalDispositions.Acknowledge(
                pending.PhysicalCommitId,
                out _))
        {
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.InputDeliveryPending,
                pending.OperationId);
            return false;
        }

        state.NextContainerFeedOperationSequence = checked(
            state.NextContainerFeedOperationSequence + 1);
        state.PendingContainerFeed = new ContainerWaterFeedState();
        state.TransferStatus = InfrastructureStatus.None;
        stateStore.Touch();
        return true;
    }

    private void RefreshWaterTransferSnapshots()
    {
        waterTransfers = topologyRuntime.Current.Nodes.Values
            .Where(node => node.Building?.BuildingData?.GetAbility<
                BuildingWaterContainerTransferAbility>() != null)
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .Select(node =>
            {
                BuildingWaterContainerTransferAbility ability =
                    node.Building.BuildingData.GetAbility<
                        BuildingWaterContainerTransferAbility>();
                FluidNodeState state = stateStore.EnsureState(node.NodeId);
                return new WaterTransferFacilitySnapshot
                {
                    BuildingId = new BuildingInstanceId(node.NodeId),
                    Mode = state.TransferMode,
                    Powered = !ability.requiresPower
                        || power.IsPowered(node.Building),
                    Progress01 = state.TransferMode
                        == WaterContainerTransferMode.Disabled
                            ? 0f
                            : Mathf.Clamp01(
                                state.TransferWork
                                / Mathf.Max(0.1f, ability.secondsPerBatch)),
                    Status = state.TransferStatus
                };
            })
            .ToArray();
    }

    private static string CreateWaterTransferDestinationId(string nodeId) =>
        $"plumbing:water-transfer:{nodeId}";

    private void EnsureTopology()
    {
        if (projectionAdapter.EnsurePublishedRestoreRevision())
        {
            ResetTransientProjection();
        }

        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!inputOwners.TryReconcile(topology, out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Fluid input destination authority reconciliation failed: "
                + ownerFailure);
        }
        if (!projectionAdapter.TryUpdateTopologyVersion(
                topology.SourceVersion))
        {
            return;
        }

        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values.Where(
                     node => (node.Channels
                             & (UtilityChannel.CleanWater
                                | UtilityChannel.Wastewater))
                         != 0))
        {
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            BuildingWaterStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>();
            if (storage != null)
            {
                float waterCapacity = Mathf.Max(
                    0f,
                    storage.cleanWaterCapacity);
                FluidNodeWaterRules.ClampToCapacity(state, waterCapacity);
                state.Wastewater = Mathf.Min(
                    state.Wastewater,
                    Mathf.Max(0f, storage.wastewaterCapacity));
            }
        }

        stateStore.Touch();
    }

    private void ProduceWater(float deltaTime)
    {
        foreach (IndustrialNodeDescriptor node in topologyRuntime.Current.Nodes
                     .Values.OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            BuildingWaterProducerAbility producer =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterProducerAbility>();
            if (producer == null
                || producer.productionPerSecond <= 0f
                || producer.requiresPower && !power.IsPowered(node.Building))
            {
                continue;
            }

            if (!projectionAdapter.TryResolveNetwork(
                    node.Building,
                    UtilityChannel.CleanWater,
                    out string networkId,
                    out _))
            {
                continue;
            }

            float rate = producer.productionPerSecond
                * ResolveFlowMultiplier(node);
            AddNetworkWater(
                networkId,
                producer.quality,
                rate * deltaTime);
        }
    }

    private void ProcessWastewater(float deltaTime)
    {
        foreach (IndustrialNodeDescriptor node in topologyRuntime.Current.Nodes
                     .Values.OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            BuildingWastewaterProcessorAbility processor =
                node.Building.BuildingData
                    .GetAbility<BuildingWastewaterProcessorAbility>();
            if (processor == null
                || processor.requiresPower && !power.IsPowered(node.Building))
            {
                continue;
            }

            if (!projectionAdapter.TryResolveNetwork(
                    node.Building,
                    UtilityChannel.Wastewater,
                    out string wasteNetworkId,
                    out _)
                || !projectionAdapter.TryResolveNetwork(
                    node.Building,
                    UtilityChannel.CleanWater,
                    out string waterNetworkId,
                    out _)
                || GetNetworkWastewater(wasteNetworkId)
                    + 0.0001f < processor.wastewaterInput
                || GetNetworkWaterFreeCapacity(waterNetworkId)
                    + 0.0001f < processor.waterOutput)
            {
                continue;
            }

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            state.ProcessorWork += deltaTime * ResolveFlowMultiplier(node);
            float required = Mathf.Max(0.1f, processor.secondsPerBatch);
            while (state.ProcessorWork + 0.0001f >= required
                   && GetNetworkWastewater(wasteNetworkId)
                       + 0.0001f >= processor.wastewaterInput
                   && GetNetworkWaterFreeCapacity(waterNetworkId)
                       + 0.0001f >= processor.waterOutput)
            {
                RemoveNetworkWastewater(
                    wasteNetworkId,
                    processor.wastewaterInput);
                AddNetworkWater(
                    waterNetworkId,
                    processor.outputQuality,
                    processor.waterOutput);
                state.ProcessorWork -= required;
                if (processor.sludgeAmount > 0
                    && !string.IsNullOrWhiteSpace(processor.sludgeItemId))
                {
                    items.SpawnItemAt(
                        processor.sludgeItemId.Trim(),
                        processor.sludgeAmount,
                        node.Building.centerPos,
                        WorldItemStackState.Loose,
                        string.Empty,
                        out _);
                }
            }
        }
    }

    private void ApplyLeaksAndBackflow(float deltaTime)
    {
        float meteringLeakMultiplier = facilities.FindOperational(
                ResearchFacilityCommandKind.FlowMetering).Count > 0
            ? 0.85f
            : 1f;
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values)
        {
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            if (state.Leak > 0f)
            {
                float leaked = Mathf.Min(
                    state.CleanWater + state.UnsafeWater + state.FoulWater,
                    state.Leak * 0.001f * deltaTime
                        * meteringLeakMultiplier);
                FluidNodeWaterRules.Remove(
                    state,
                    WorldWaterQuality.Foul,
                    leaked);
                if (leaked > 0.02f)
                {
                    filth.AddFilth(
                        WorldFilthType.Sewage,
                        node.Building.centerPos,
                        leaked * 2f,
                        string.Empty,
                        0.25f);
                }
            }
        }

        if (!topology.NodesByNetwork.TryGetValue(
                UtilityChannel.Wastewater,
                out Dictionary<string, List<string>> networksByNode))
        {
            return;
        }

        foreach (KeyValuePair<string, List<string>> network in networksByNode)
        {
            float capacity = GetWastewaterCapacity(network.Key);
            if (capacity <= 0f
                || GetNetworkWastewater(network.Key)
                    < capacity - 0.001f
                || clock.Time < nextBackflowAt.GetValueOrDefault(
                    network.Key,
                    0f))
            {
                continue;
            }

            IndustrialNodeDescriptor target = network.Value
                .Where(topology.Nodes.ContainsKey)
                .Select(id => topology.Nodes[id])
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target != null)
            {
                CreateBackflow(target.Building, 1f);
            }

            nextBackflowAt[network.Key] =
                clock.Time + BackflowWarningInterval;
        }
    }

    private void CreateBackflow(BuildableObject building, float amount)
    {
        if (building == null || amount <= 0f)
        {
            return;
        }

        if (projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            float previous = state.Blockage;
            state.Blockage = Mathf.Clamp(
                state.Blockage + Mathf.Max(1f, amount * 2f),
                0f,
                100f);
            if (!Mathf.Approximately(previous, state.Blockage))
                facilityStateChanges.MarkDynamicStateDirty();
        }

        filth.AddFilth(
            WorldFilthType.Sewage,
            building.centerPos,
            Mathf.Max(2f, amount * 8f),
            string.Empty,
            Mathf.Clamp01(0.35f + amount * 0.05f));
    }

    private float ResolveFlowMultiplier(IndustrialNodeDescriptor node)
    {
        FluidNodeState state = stateStore.EnsureState(node.NodeId);
        return Mathf.Clamp01(
            1f - state.Blockage / 100f - state.FaultEquivalent());
    }

    private float AddNetworkWater(
        string networkId,
        WorldWaterQuality quality,
        float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
                     UtilityChannel.CleanWater,
                     networkId))
        {
            BuildingWaterStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>();
            if (storage == null || storage.cleanWaterCapacity <= 0f)
            {
                continue;
            }

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            float free = Mathf.Max(
                0f,
                storage.cleanWaterCapacity
                - state.CleanWater
                - state.UnsafeWater
                - state.FoulWater);
            float accepted = Mathf.Min(free, remaining);
            FluidNodeWaterRules.Add(state, quality, accepted);
            remaining -= accepted;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }

        return amount - remaining;
    }

    private float GetNetworkWater(
        string networkId,
        WorldWaterQuality quality)
    {
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.CleanWater,
                networkId)
            .Sum(node => FluidNodeWaterRules.GetWater(
                stateStore.EnsureState(node.NodeId),
                quality));
    }

    private float GetNetworkWaterFreeCapacity(string networkId)
    {
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.CleanWater,
                networkId)
            .Sum(node =>
            {
                BuildingWaterStorageAbility storage =
                    node.Building.BuildingData
                        .GetAbility<BuildingWaterStorageAbility>();
                FluidNodeState state = stateStore.EnsureState(node.NodeId);
                return storage == null
                    ? 0f
                    : Mathf.Max(
                        0f,
                        storage.cleanWaterCapacity
                        - state.CleanWater
                        - state.UnsafeWater
                        - state.FoulWater);
            });
    }

    private void RemoveNetworkWater(
        string networkId,
        WorldWaterQuality quality,
        float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
                     UtilityChannel.CleanWater,
                     networkId))
        {
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            float removed = Mathf.Min(
                FluidNodeWaterRules.GetWater(state, quality),
                remaining);
            FluidNodeWaterRules.SetWater(
                state,
                quality,
                FluidNodeWaterRules.GetWater(state, quality) - removed);
            remaining -= removed;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }
    }

    private float GetNetworkWastewater(string networkId)
    {
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.Wastewater,
                networkId)
            .Sum(node => stateStore.EnsureState(node.NodeId).Wastewater);
    }

    private float GetWastewaterCapacity(string networkId)
    {
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.Wastewater,
                networkId)
            .Sum(node => Mathf.Max(
                0f,
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>()
                    ?.wastewaterCapacity
                ?? 0f));
    }

    private void AddNetworkWastewater(string networkId, float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
                     UtilityChannel.Wastewater,
                     networkId))
        {
            BuildingWaterStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>();
            if (storage == null || storage.wastewaterCapacity <= 0f)
            {
                continue;
            }

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            float accepted = Mathf.Min(
                Mathf.Max(
                    0f,
                    storage.wastewaterCapacity - state.Wastewater),
                remaining);
            state.Wastewater += accepted;
            remaining -= accepted;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }
    }

    private float RemoveNetworkWastewater(string networkId, float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
                     UtilityChannel.Wastewater,
                     networkId))
        {
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            float removed = Mathf.Min(state.Wastewater, remaining);
            state.Wastewater -= removed;
            remaining -= removed;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }

        return amount - remaining;
    }

    private void ResetProjectionAfterRestore()
    {
        projectionAdapter.Reset(stateStore.PublishedRestoreRevision);
        ResetTransientProjection();
    }

    private void ResetTransientProjection()
    {
        accumulated = 0f;
        nextBackflowAt.Clear();
        waterTransfers = Array.Empty<WaterTransferFacilitySnapshot>();
    }
}
