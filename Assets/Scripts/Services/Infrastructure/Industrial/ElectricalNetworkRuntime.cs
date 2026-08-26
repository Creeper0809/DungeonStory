using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class ElectricalNodeState
{
    public PowerPriority Priority = PowerPriority.Production;
    public float StoredPower;
    public float FuelSeconds;
    public float Heat;
    public float Fault;
    public bool BreakerTripped;
    public bool Powered;
    public float SuppliedFraction;
    public int NextFuelOperationSequence = 1;
    public PowerFuelCommitSaveData PendingFuel = new PowerFuelCommitSaveData();
}

internal sealed class ElectricalNetworkSummaryState
{
    public float ProductionPerSecond;
    public float DemandPerSecond;
    public float SuppliedPerSecond;
    public bool Tripped;
}

internal readonly struct ElectricalConsumerEntry
{
    public ElectricalConsumerEntry(
        IndustrialNodeDescriptor node,
        BuildingPowerConsumerAbility ability)
    {
        Node = node;
        Ability = ability;
    }

    public IndustrialNodeDescriptor Node { get; }
    public BuildingPowerConsumerAbility Ability { get; }
}

internal sealed class ElectricalNetworkRuntime :
    IPowerInfrastructureQuery,
    IPowerInfrastructureCommand,
    IPowerInfrastructurePersistence,
    ITickable
{
    private const float TickInterval = 0.25f;
    private const float FuelRequestInterval = 10f;
    private const int FuelBufferBatchCapacity = 4;
    private const string FuelBufferOwnerDomain = "infrastructure.electrical";
    // Authored semantic revision for the fuel-buffer capacity contract. This is
    // deliberately independent from the transient industrial topology epoch so
    // live projection and save restore publish the same profile identity.
    internal const long FuelBufferCapacitySchemaRevision = 1L;
    internal const string FuelDispositionReasonCode =
        "power-generator-fuel-combustion";

    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IGameClock clock;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalFacilityItemSinkGateway physicalFuel;
    private readonly AutomationPowerDemandRegistry automationPowerDemand;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IFacilityBufferDestinationLifecycleCommand bufferLifecycle;
    private readonly IFacilityBufferDestinationClaimQuery bufferClaims;
    private readonly IFacilityBufferDestinationReleaseService bufferRelease;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly Dictionary<string, float> nextFuelRequestAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, ElectricalNetworkSummaryState>
        networkSummaries =
            new Dictionary<string, ElectricalNetworkSummaryState>(
                StringComparer.Ordinal);
    private readonly List<ElectricalConsumerEntry> consumerScratch =
        new List<ElectricalConsumerEntry>(64);
    private IReadOnlyList<PowerNetworkSnapshot> networks =
        Array.Empty<PowerNetworkSnapshot>();
    private float accumulated;
    private int topologyVersion = int.MinValue;
    private int automationPowerVersion = int.MinValue;
    private int projectedRestoreRevision;

    private ElectricalNetworkAggregateState State =>
        aggregateRootStore.GetOrCreateWritable(
            () => new ElectricalNetworkAggregateState(),
            state => state.DeepClone());

    private Dictionary<string, ElectricalNodeState> states => State.Nodes;

    public ElectricalNetworkRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IGameClock clock,
        IWorldItemStackRuntime items,
        IPhysicalFacilityItemSinkGateway physicalFuel,
        AutomationPowerDemandRegistry automationPowerDemand,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IFacilityBufferDestinationLifecycleCommand bufferLifecycle,
        IFacilityBufferDestinationClaimQuery bufferClaims,
        IFacilityBufferDestinationReleaseService bufferRelease,
        IMilestoneGameplayModifierQuery milestoneModifiers = null)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.physicalFuel = physicalFuel
            ?? throw new ArgumentNullException(nameof(physicalFuel));
        this.automationPowerDemand = automationPowerDemand
            ?? throw new ArgumentNullException(nameof(automationPowerDemand));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.bufferLifecycle = bufferLifecycle
            ?? throw new ArgumentNullException(nameof(bufferLifecycle));
        this.bufferClaims = bufferClaims
            ?? throw new ArgumentNullException(nameof(bufferClaims));
        this.bufferRelease = bufferRelease
            ?? throw new ArgumentNullException(nameof(bufferRelease));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;
        projectedRestoreRevision =
            this.aggregateRootStore.PublishedRestoreRevision;
    }

    public int Version => State.Version;
    public IReadOnlyList<PowerNetworkSnapshot> Networks
    {
        get
        {
            EnsureTopology();
            RefreshSnapshots();
            return networks;
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
        EvaluateNetworks(deltaTime);
    }

    public bool IsPowered(BuildableObject building)
    {
        if (!TryResolve(building, out string nodeId, out _))
        {
            return false;
        }

        return states.TryGetValue(nodeId, out ElectricalNodeState state)
            && state.Powered
            && !state.BreakerTripped;
    }

    public bool TryGetNode(
        BuildableObject building,
        out PowerNodeSnapshot snapshot)
    {
        snapshot = null;
        if (!TryResolve(building, out string nodeId, out IndustrialNodeDescriptor node)
            || !states.TryGetValue(nodeId, out ElectricalNodeState state))
        {
            return false;
        }

        snapshot = CreateNodeSnapshot(
            node,
            state,
            ResolvePowerNetworkId(nodeId));
        return true;
    }

    public InfrastructureCommandResult SetPriority(
        BuildableObject building,
        PowerPriority priority)
    {
        if (!Enum.IsDefined(typeof(PowerPriority), priority)
            || !TryResolve(building, out string nodeId, out IndustrialNodeDescriptor node)
            || node.Building.BuildingData
                .GetAbility<BuildingPowerConsumerAbility>() == null)
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.PowerConsumerUnavailable);
        }

        EnsureState(node).Priority = priority;
        Touch();
        EvaluateNetworks(0f);
        return InfrastructureCommandResult.Success();
    }

    public InfrastructureCommandResult ResetBreaker(
        BuildableObject building)
    {
        if (!TryResolve(building, out string nodeId, out IndustrialNodeDescriptor node)
            || node.Building.BuildingData
                .GetAbility<BuildingCircuitBreakerAbility>() == null)
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.PowerBreakerUnavailable);
        }

        ElectricalNodeState state = EnsureState(node);
        if (state.Heat >= 60f)
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.PowerBreakerUnavailable,
                state.Heat.ToString("0.###"));
        }

        state.BreakerTripped = false;
        state.Fault = Mathf.Max(0f, state.Fault - 10f);
        Touch();
        EvaluateNetworks(0f);
        return InfrastructureCommandResult.Success();
    }

    public DungeonPowerInfrastructureSaveData Capture()
    {
        EnsureTopology();
        return new DungeonPowerInfrastructureSaveData
        {
            nodes = states
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new PowerNodeSaveData
                {
                    buildingInstanceId = pair.Key,
                    priority = (int)pair.Value.Priority,
                    storedPower = pair.Value.StoredPower,
                    fuelSeconds = pair.Value.FuelSeconds,
                    heat = pair.Value.Heat,
                    fault = pair.Value.Fault,
                    breakerTripped = pair.Value.BreakerTripped,
                    nextFuelOperationSequence =
                        pair.Value.NextFuelOperationSequence,
                    pendingFuel = pair.Value.PendingFuel?.Clone()
                        ?? new PowerFuelCommitSaveData()
                })
                .ToList()
        };
    }

    public ElectricalNetworkRestoreCandidate PrepareRestore(
        DungeonPowerInfrastructureSaveData snapshot)
    {
        IndustrialInfrastructureSaveValidation.RequireValid(snapshot);
        ElectricalNetworkAggregateState restored =
            new ElectricalNetworkAggregateState
            {
                Version = 1
            };
        foreach (PowerNodeSaveData saved in snapshot?.nodes
                 ?? new List<PowerNodeSaveData>())
        {
            if (saved == null
                || !new BuildingInstanceId(
                    saved.buildingInstanceId).IsValid)
            {
                continue;
            }

            PowerPriority priority =
                Enum.IsDefined(typeof(PowerPriority), saved.priority)
                    ? (PowerPriority)saved.priority
                    : PowerPriority.Production;
            restored.Nodes[saved.buildingInstanceId.Trim()] =
                new ElectricalNodeState
            {
                Priority = priority,
                StoredPower = Mathf.Max(0f, saved.storedPower),
                FuelSeconds = Mathf.Max(0f, saved.fuelSeconds),
                Heat = Mathf.Max(0f, saved.heat),
                Fault = Mathf.Clamp(saved.fault, 0f, 100f),
                BreakerTripped = saved.breakerTripped,
                NextFuelOperationSequence = saved.nextFuelOperationSequence,
                PendingFuel = saved.pendingFuel?.Clone()
                    ?? new PowerFuelCommitSaveData()
            };
        }

        return new ElectricalNetworkRestoreCandidate(restored);
    }

    public void Restore(ElectricalNetworkRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        aggregateRootStore.Replace(candidate.State);
        if (aggregateRootStore.IsRestoreStaging)
        {
            // The detached facility candidate is already indexed before save
            // stages commit. Publish the power owner into the claim/profile
            // restore candidates so carried fuel can rebind at participant 225.
            topologyRuntime.MarkDirty();
            PublishFuelBufferAuthorities(topologyRuntime.Current);
        }
        else
        {
            ResetProjectionAfterRestore();
            EnsureTopology();
            EvaluateNetworks(0f);
        }
    }

    private void EnsureTopology()
    {
        EnsureRestoreProjectionCurrent();
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        bool topologyChanged = topology.SourceVersion != topologyVersion;
        bool automationChanged =
            automationPowerDemand.Version != automationPowerVersion;
        if (!topologyChanged && !automationChanged)
        {
            return;
        }

        if (!topologyChanged)
        {
            EvaluateNetworks(0f);
            automationPowerVersion = automationPowerDemand.Version;
            return;
        }

        networkSummaries.Clear();
        PublishFuelBufferAuthorities(topology);
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values
                     .Where(node => (node.Channels & UtilityChannel.Power) != 0))
        {
            ElectricalNodeState state = EnsureState(node);
            BuildingPowerStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerStorageAbility>();
            if (storage != null)
            {
                state.StoredPower = Mathf.Clamp(
                    state.StoredPower,
                    0f,
                    storage.capacity);
            }
        }

        EvaluateNetworks(0f);
        topologyVersion = topology.SourceVersion;
        automationPowerVersion = automationPowerDemand.Version;
        Touch();
    }

    private void PublishFuelBufferAuthorities(IndustrialTopologySnapshot topology)
    {
        IEnumerable<IndustrialNodeDescriptor> topologyNodes = topology == null
            ? Enumerable.Empty<IndustrialNodeDescriptor>()
            : topology.Nodes.Values;
        IndustrialNodeDescriptor[] fueledNodes = topologyNodes
            .Where(node => node?.Building != null)
            .Where(node => node.Building.BuildingData
                .GetAbility<BuildingPowerProducerAbility>() is
                { requiresFuel: true } producer
                && !string.IsNullOrWhiteSpace(producer.fuelItemId))
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        List<FacilityBufferDestinationClaim> claims = new(fueledNodes.Length);
        List<FacilityBufferCapacityProfile> profiles = new(fueledNodes.Length);
        foreach (IndustrialNodeDescriptor node in fueledNodes)
        {
            BuildingPowerProducerAbility producer = node.Building.BuildingData
                .GetAbility<BuildingPowerProducerAbility>();
            string fuelItemId = producer.fuelItemId;
            if (!string.Equals(fuelItemId, fuelItemId.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Power node '{node.NodeId}' has a non-canonical fuel item id.");
            }
            string destinationId = ReservedTargetDestinationIdentity.PowerFuelPrefix
                + node.NodeId;
            string facilityId = node.Building.PersistentInstanceId.Value;
            long unitMassGrams = items.MassQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)fuelItemId).Value;
            long maxMassGrams = checked(
                unitMassGrams * FuelBufferBatchCapacity);
            claims.Add(new FacilityBufferDestinationClaim(
                destinationId,
                node.Building.centerPos,
                FuelBufferOwnerDomain,
                destinationId,
                facilityId,
                FacilityBufferDestinationAnchorKind.LiveBuilding));
            profiles.Add(new FacilityBufferCapacityProfile(
                destinationId,
                node.Building.centerPos,
                FuelBufferOwnerDomain,
                destinationId,
                facilityId,
                new PhysicalMassGrams(maxMassGrams),
                FuelBufferCapacitySchemaRevision));
        }

        if (!aggregateRootStore.IsRestoreStaging)
        {
            HashSet<string> desiredDestinations = claims
                .Select(value => value.DestinationId)
                .ToHashSet(StringComparer.Ordinal);
            FacilityBufferDestinationClaim[] retiredClaims = bufferClaims
                .CaptureClaims()
                .Where(value => string.Equals(
                    value.OwnerDomain,
                    FuelBufferOwnerDomain,
                    StringComparison.Ordinal))
                .Where(value => !desiredDestinations.Contains(value.DestinationId))
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();
            foreach (FacilityBufferDestinationClaim retired in retiredClaims)
            {
                if (!bufferRelease.TryReleaseAtOwnerPosition(
                        retired.DestinationId,
                        retired.DropPosition,
                        "power-fuel-owner-retired",
                        out _,
                        out string releaseFailure))
                {
                    throw new InvalidOperationException(
                        $"Power fuel buffer terminal release failed for "
                        + $"'{retired.DestinationId}': {releaseFailure}");
                }
            }
        }

        if (!bufferLifecycle.TryReplaceOwnedAuthorities(
                FuelBufferOwnerDomain,
                claims,
                profiles,
                out string failureReason))
        {
            throw new InvalidOperationException(
                $"Power fuel buffer authority publication failed: {failureReason}");
        }
    }

    private void EvaluateNetworks(float deltaTime)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.NodeDescriptorsByNetwork.TryGetValue(
                UtilityChannel.Power,
                out Dictionary<
                    string,
                    IReadOnlyList<IndustrialNodeDescriptor>> grouped))
        {
            networkSummaries.Clear();
            networks = Array.Empty<PowerNetworkSnapshot>();
            return;
        }

        foreach (KeyValuePair<
                     string,
                     IReadOnlyList<IndustrialNodeDescriptor>> network
                 in grouped)
        {
            EvaluateNetwork(
                network.Key,
                network.Value,
                deltaTime);
        }

        Touch();
    }

    private void EvaluateNetwork(
        string networkId,
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float deltaTime)
    {
        bool tripped = false;
        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            if (node.Building.BuildingData
                    .GetAbility<BuildingCircuitBreakerAbility>() != null
                && EnsureState(node).BreakerTripped)
            {
                tripped = true;
                break;
            }
        }

        float production = 0f;
        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            BuildingPowerProducerAbility producer =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerProducerAbility>();
            if (producer == null
                || tripped
                || !CanProduce(node, producer, deltaTime))
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            production += Mathf.Max(0f, producer.productionPerSecond)
                * Mathf.Clamp01(1f - state.Fault / 125f);
        }

        consumerScratch.Clear();
        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            BuildingPowerConsumerAbility ability =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerConsumerAbility>();
            if (ability != null)
            {
                consumerScratch.Add(
                    new ElectricalConsumerEntry(node, ability));
            }
        }

        consumerScratch.Sort(CompareConsumers);
        float demand = 0f;
        for (int index = 0; index < consumerScratch.Count; index++)
        {
            ElectricalConsumerEntry consumer = consumerScratch[index];
            demand += ResolveDemand(consumer.Node, consumer.Ability);
        }

        float dischargeRate = tripped
            ? 0f
            : ResolveDischargeRate(nodes, deltaTime, demand - production);
        float available = tripped ? 0f : production + dischargeRate;
        float supplied = 0f;
        for (int index = 0; index < consumerScratch.Count; index++)
        {
            ElectricalConsumerEntry consumer = consumerScratch[index];
            IndustrialNodeDescriptor node = consumer.Node;
            BuildingPowerConsumerAbility ability = consumer.Ability;
            ElectricalNodeState state = EnsureState(node);
            float requested = ResolveDemand(node, ability);
            float granted = Mathf.Min(requested, Mathf.Max(0f, available));
            float fraction = requested <= 0.001f ? 1f : granted / requested;
            state.SuppliedFraction = fraction;
            state.Powered = fraction + 0.001f
                >= Mathf.Clamp01(ability.minimumSupplyFraction);
            if (state.Powered)
            {
                available -= granted;
                supplied += granted;
            }
        }

        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            if (node.Building.BuildingData
                    .GetAbility<BuildingPowerConsumerAbility>() != null)
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            state.Powered = !tripped && production + dischargeRate > 0.001f;
            state.SuppliedFraction = state.Powered ? 1f : 0f;
        }

        float excess = Mathf.Max(0f, production - supplied);
        ChargeStorage(nodes, excess, deltaTime);
        UpdateOverload(nodes, production, demand, deltaTime);

        if (!networkSummaries.TryGetValue(
                networkId,
                out ElectricalNetworkSummaryState summary))
        {
            summary = new ElectricalNetworkSummaryState();
            networkSummaries[networkId] = summary;
        }

        summary.ProductionPerSecond = production;
        summary.DemandPerSecond = demand;
        summary.SuppliedPerSecond = supplied;
        summary.Tripped = tripped;
    }

    private int CompareConsumers(
        ElectricalConsumerEntry left,
        ElectricalConsumerEntry right)
    {
        int priorityComparison = ((int)EnsureState(left.Node).Priority)
            .CompareTo((int)EnsureState(right.Node).Priority);
        return priorityComparison != 0
            ? priorityComparison
            : string.CompareOrdinal(left.Node.NodeId, right.Node.NodeId);
    }

    private void RefreshSnapshots()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.NodeDescriptorsByNetwork.TryGetValue(
                UtilityChannel.Power,
                out Dictionary<
                    string,
                    IReadOnlyList<IndustrialNodeDescriptor>> grouped))
        {
            networks = Array.Empty<PowerNetworkSnapshot>();
            return;
        }

        List<PowerNetworkSnapshot> snapshots =
            new List<PowerNetworkSnapshot>(grouped.Count);
        foreach (KeyValuePair<
                     string,
                     IReadOnlyList<IndustrialNodeDescriptor>> network
                 in grouped.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            List<PowerNodeSnapshot> nodeSnapshots =
                new List<PowerNodeSnapshot>(network.Value.Count);
            float storedPower = 0f;
            float storageCapacity = 0f;
            for (int index = 0; index < network.Value.Count; index++)
            {
                IndustrialNodeDescriptor node = network.Value[index];
                PowerNodeSnapshot nodeSnapshot = CreateNodeSnapshot(
                    node,
                    EnsureState(node),
                    network.Key);
                nodeSnapshots.Add(nodeSnapshot);
                storedPower += nodeSnapshot.StoredPower;
                storageCapacity += nodeSnapshot.StorageCapacity;
            }

            networkSummaries.TryGetValue(
                network.Key,
                out ElectricalNetworkSummaryState summary);
            snapshots.Add(new PowerNetworkSnapshot
            {
                NetworkId = network.Key,
                ProductionPerSecond =
                    summary?.ProductionPerSecond ?? 0f,
                DemandPerSecond = summary?.DemandPerSecond ?? 0f,
                SuppliedPerSecond = summary?.SuppliedPerSecond ?? 0f,
                StoredPower = storedPower,
                StorageCapacity = storageCapacity,
                Tripped = summary?.Tripped ?? false,
                Nodes = nodeSnapshots
            });
        }

        networks = snapshots;
    }

    private bool CanProduce(
        IndustrialNodeDescriptor node,
        BuildingPowerProducerAbility producer,
        float deltaTime)
    {
        if (!producer.requiresFuel)
        {
            return true;
        }

        ElectricalNodeState state = EnsureState(node);
        bool recoveryCompleted = TryRecoverFuelCommit(
            node,
            producer,
            state,
            out bool fuelUsable);
        if (!recoveryCompleted && !fuelUsable)
        {
            return false;
        }
        state.FuelSeconds = Mathf.Max(0f, state.FuelSeconds - deltaTime);
        if (state.FuelSeconds > 0f)
        {
            return true;
        }
        if (!recoveryCompleted)
        {
            return false;
        }

        string fuelItemId = producer.fuelItemId?.Trim() ?? string.Empty;
        string destinationId = "power:" + node.NodeId;
        if (!string.IsNullOrWhiteSpace(fuelItemId)
            && TryBeginFuelCommit(
                node,
                producer,
                state,
                fuelItemId,
                destinationId))
        {
            return true;
        }

        if (clock.Time >= nextFuelRequestAt.GetValueOrDefault(node.NodeId, 0f)
            && !string.IsNullOrWhiteSpace(fuelItemId))
        {
            items.TryRequestItemDelivery(
                fuelItemId,
                1,
                node.Building.centerPos,
                destinationId,
                out _,
                out _);
            nextFuelRequestAt[node.NodeId] =
                clock.Time + FuelRequestInterval;
        }

        return false;
    }

    internal static string FormatFuelOperationId(
        string nodeId,
        int sequence) => $"power-fuel:{nodeId}:{sequence:D8}";

    private bool TryBeginFuelCommit(
        IndustrialNodeDescriptor node,
        BuildingPowerProducerAbility producer,
        ElectricalNodeState state,
        string fuelItemId,
        string destinationId)
    {
        int sequence = state.NextFuelOperationSequence;
        string operationId = FormatFuelOperationId(node.NodeId, sequence);
        state.PendingFuel = new PowerFuelCommitSaveData
        {
            phase = (int)PowerFuelCommitPhase.IntentRecorded,
            operationSequence = sequence,
            operationId = operationId,
            reasonCode = FuelDispositionReasonCode,
            nodeId = node.NodeId,
            destinationId = destinationId,
            itemId = fuelItemId,
            quantity = 1,
            fuelSecondsBefore = 0f,
            fuelSecondsAfter = Mathf.Max(1f, producer.secondsPerFuel)
        };
        Touch();

        if (!physicalFuel.TryCommitSinkPending(
                destinationId,
                fuelItemId,
                1,
                operationId,
                FuelDispositionReasonCode,
                out _,
                out _))
        {
            ClearFuelCommit(state, advanceSequence: false);
            Touch();
            return false;
        }

        TryRecoverFuelCommit(node, producer, state, out bool fuelUsable);
        return fuelUsable;
    }

    private bool TryRecoverFuelCommit(
        IndustrialNodeDescriptor node,
        BuildingPowerProducerAbility producer,
        ElectricalNodeState state,
        out bool fuelUsable)
    {
        fuelUsable = state.FuelSeconds > 0f;
        PowerFuelCommitSaveData pending = state.PendingFuel
            ?? new PowerFuelCommitSaveData();
        PowerFuelCommitPhase phase = (PowerFuelCommitPhase)pending.phase;
        if (phase == PowerFuelCommitPhase.None)
        {
            return true;
        }

        string authoredItemId = producer.fuelItemId?.Trim() ?? string.Empty;
        string destinationId = "power:" + node.NodeId;
        float expectedAfter = Mathf.Max(1f, producer.secondsPerFuel);
        bool contractMatches = pending.operationSequence
                == state.NextFuelOperationSequence
            && pending.quantity == 1
            && string.Equals(pending.nodeId, node.NodeId, StringComparison.Ordinal)
            && string.Equals(
                pending.destinationId,
                destinationId,
                StringComparison.Ordinal)
            && string.Equals(pending.itemId, authoredItemId, StringComparison.Ordinal)
            && string.Equals(
                pending.reasonCode,
                FuelDispositionReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                pending.operationId,
                FormatFuelOperationId(node.NodeId, pending.operationSequence),
                StringComparison.Ordinal)
            && Mathf.Approximately(pending.fuelSecondsBefore, 0f)
            && Mathf.Approximately(pending.fuelSecondsAfter, expectedAfter);
        if (!contractMatches)
        {
            throw new InvalidOperationException(
                $"Power fuel commit '{pending.operationId}' conflicts with node '{node.NodeId}'.");
        }

        bool hasReceipt = physicalFuel.TryGetPending(
            pending.operationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (hasReceipt
            && (!receipt.IsCommitted
                || receipt.Kind != PhysicalItemDispositionKind.Sink
                || receipt.Quantity != 1
                || !string.Equals(
                    receipt.OperationId,
                    pending.operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.ReasonCode,
                    pending.reasonCode,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Power fuel commit '{pending.operationId}' has a mismatched physical receipt.");
        }

        if (phase == PowerFuelCommitPhase.IntentRecorded)
        {
            if (!hasReceipt)
            {
                ClearFuelCommit(state, advanceSequence: false);
                Touch();
                return true;
            }

            state.FuelSeconds = pending.fuelSecondsAfter;
            pending.phase = (int)PowerFuelCommitPhase.OutcomePublished;
            pending.sourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            pending.inputMassGrams = receipt.InputMassGrams;
            pending.commitId = receipt.CommitId;
            Touch();
            fuelUsable = true;
        }
        else
        {
            if (!hasReceipt)
            {
                throw new InvalidOperationException(
                    $"Power fuel commit '{pending.operationId}' lost its published physical receipt.");
            }
            fuelUsable = state.FuelSeconds > 0f;
        }

        if (hasReceipt
            && !physicalFuel.Acknowledge(receipt.CommitId, out _))
        {
            return false;
        }

        ClearFuelCommit(state, advanceSequence: true);
        Touch();
        return true;
    }

    private static void ClearFuelCommit(
        ElectricalNodeState state,
        bool advanceSequence)
    {
        if (advanceSequence)
        {
            state.NextFuelOperationSequence = checked(
                state.NextFuelOperationSequence + 1);
        }
        state.PendingFuel = new PowerFuelCommitSaveData();
    }

    private float ResolveDischargeRate(
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float deltaTime,
        float requestedRate)
    {
        if (requestedRate <= 0f || deltaTime <= 0f)
        {
            return 0f;
        }

        float remainingEnergy = requestedRate * deltaTime;
        float suppliedEnergy = 0f;
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            BuildingPowerStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerStorageAbility>();
            if (storage == null)
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            float available = Mathf.Min(
                state.StoredPower,
                storage.transferPerSecond * deltaTime);
            float removed = Mathf.Min(available, remainingEnergy);
            state.StoredPower -= removed;
            remainingEnergy -= removed;
            suppliedEnergy += removed * ResolveStorageEfficiency(storage);
            if (remainingEnergy <= 0.001f)
            {
                break;
            }
        }

        return suppliedEnergy / deltaTime;
    }

    private void ChargeStorage(
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float excessRate,
        float deltaTime)
    {
        if (excessRate <= 0f || deltaTime <= 0f)
        {
            return;
        }

        float energy = excessRate * deltaTime;
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            BuildingPowerStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerStorageAbility>();
            if (storage == null)
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            float room = Mathf.Max(0f, storage.capacity - state.StoredPower);
            float input = Mathf.Min(
                energy,
                storage.transferPerSecond * deltaTime);
            float stored = Mathf.Min(
                room,
                input * ResolveStorageEfficiency(storage));
            state.StoredPower += stored;
            energy -= input;
            if (energy <= 0.001f)
            {
                break;
            }
        }
    }

    private void UpdateOverload(
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float production,
        float demand,
        float deltaTime)
    {
        float capacity = Mathf.Max(0.01f, production);
        float ratio = demand / capacity;
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            ElectricalNodeState state = EnsureState(node);
            state.Heat = ratio > 1f
                ? state.Heat + (ratio - 1f) * 18f * deltaTime
                : Mathf.Max(0f, state.Heat - 8f * deltaTime);
            if (state.Heat > 75f)
            {
                state.Fault = Mathf.Clamp(
                    state.Fault + (state.Heat - 75f) * 0.02f * deltaTime,
                    0f,
                    100f);
            }

            BuildingCircuitBreakerAbility breaker =
                node.Building.BuildingData
                    .GetAbility<BuildingCircuitBreakerAbility>();
            if (breaker != null
                && ratio > Mathf.Max(1f, breaker.overloadTolerance)
                && state.Heat >= Mathf.Max(1f, breaker.tripHeat))
            {
                state.BreakerTripped = true;
                state.Powered = false;
            }
        }
    }

    private float ResolveStorageEfficiency(BuildingPowerStorageAbility storage)
    {
        float authored = Mathf.Clamp01(storage?.efficiency ?? 0f);
        float remainingLoss = (1f - authored) * Mathf.Clamp(
            milestoneModifiers.ManaTransferLossMultiplier,
            0f,
            1f);
        return Mathf.Clamp01(1f - remainingLoss);
    }

    private ElectricalNodeState EnsureState(IndustrialNodeDescriptor node)
    {
        if (!states.TryGetValue(node.NodeId, out ElectricalNodeState state))
        {
            BuildingPowerConsumerAbility consumer =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerConsumerAbility>();
            state = new ElectricalNodeState
            {
                Priority = consumer?.priority ?? PowerPriority.Production
            };
            states[node.NodeId] = state;
        }

        return state;
    }

    private bool TryResolve(
        BuildableObject building,
        out string nodeId,
        out IndustrialNodeDescriptor node)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (building != null
            && topology.NodeIdsByBuilding.TryGetValue(building, out nodeId)
            && topology.Nodes.TryGetValue(nodeId, out node)
            && (node.Channels & UtilityChannel.Power) != 0)
        {
            return true;
        }

        nodeId = string.Empty;
        node = null;
        return false;
    }

    private string ResolvePowerNetworkId(string nodeId)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        return topology.NetworkByNode.TryGetValue(
                UtilityChannel.Power,
                out Dictionary<string, string> networkByNode)
            && networkByNode.TryGetValue(nodeId, out string networkId)
                ? networkId
                : string.Empty;
    }

    private PowerNodeSnapshot CreateNodeSnapshot(
        IndustrialNodeDescriptor node,
        ElectricalNodeState state,
        string networkId)
    {
        BuildingSO data = node.Building.BuildingData;
        BuildingPowerProducerAbility producer =
            data.GetAbility<BuildingPowerProducerAbility>();
        BuildingPowerConsumerAbility consumer =
            data.GetAbility<BuildingPowerConsumerAbility>();
        BuildingPowerStorageAbility storage =
            data.GetAbility<BuildingPowerStorageAbility>();
        return new PowerNodeSnapshot
        {
            BuildingId = new BuildingInstanceId(node.NodeId),
            NetworkId = networkId,
            Priority = state.Priority,
            Powered = state.Powered,
            BreakerTripped = state.BreakerTripped,
            ProductionPerSecond = producer?.productionPerSecond ?? 0f,
            DemandPerSecond = consumer == null
                ? 0f
                : ResolveDemand(node, consumer),
            SuppliedFraction = state.SuppliedFraction,
            StoredPower = state.StoredPower,
            StorageCapacity = storage?.capacity ?? 0f,
            Heat = state.Heat,
            Fault = state.Fault
        };
    }

    private float ResolveDemand(
        IndustrialNodeDescriptor node,
        BuildingPowerConsumerAbility consumer)
    {
        BuildingAutomationAbility automation =
            node.Building.BuildingData
                .GetAbility<BuildingAutomationAbility>();
        if (automation == null)
        {
            return Mathf.Max(0f, consumer.demandPerSecond);
        }

        return automationPowerDemand.ResolveDemand(
            node.NodeId,
            automation.PowerDemandProfile);
    }

    private void Touch()
    {
        unchecked
        {
            State.Version++;
        }
    }

    private void EnsureRestoreProjectionCurrent()
    {
        int revision = aggregateRootStore.PublishedRestoreRevision;
        if (projectedRestoreRevision == revision)
        {
            return;
        }

        projectedRestoreRevision = revision;
        ResetProjectionAfterRestore();
    }

    private void ResetProjectionAfterRestore()
    {
        topologyVersion = int.MinValue;
        automationPowerVersion = int.MinValue;
        accumulated = 0f;
        nextFuelRequestAt.Clear();
        networkSummaries.Clear();
        consumerScratch.Clear();
        networks = Array.Empty<PowerNetworkSnapshot>();
    }
}
