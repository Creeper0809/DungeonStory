#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Editor-only contract probe kept in the runtime assembly so it can exercise
/// the internal industrial topology without exposing that topology to the
/// Editor assembly. All item custody still goes through injected production
/// ports and the real physical repository.
/// </summary>
public static class IndustrialPowerFuelOutboxContractProbe
{
    public static string Run(
        BuildableObject building,
        IWorldItemStackRuntime items,
        IPhysicalFacilityItemSinkGateway physicalFuel,
        string fuelItemId,
        float secondsPerFuel)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (physicalFuel == null)
            throw new ArgumentNullException(nameof(physicalFuel));
        if (string.IsNullOrWhiteSpace(fuelItemId)
            || !string.Equals(
                fuelItemId,
                fuelItemId.Trim(),
                StringComparison.Ordinal)
            || secondsPerFuel <= 10f)
        {
            throw new ArgumentException(
                "A canonical fuel item and more than ten seconds per fuel are required.");
        }

        string nodeId = IndustrialInfrastructureIdentity.GetNodeId(building);
        string destinationId = "power:" + nodeId;
        int beforeQuantity = CountFuel(items, destinationId, fuelItemId);
        Require(beforeQuantity >= 2,
            "Power fuel probe requires at least two physical fuel items.");
        IndustrialTopologySnapshot snapshot =
            IndustrialInfrastructureTopologyBuilder.Build(
                1,
                new[] { building });
        Require(snapshot.Nodes.ContainsKey(nodeId),
            "Power fuel probe building was not captured by industrial topology.");
        FixedTopology topology = new(snapshot);
        RecordingFacilityBufferLifecycle firstBufferLifecycle = new();
        ProbeClock clock = new();
        DungeonRuntimeAggregateRootStore firstStore = new();
        ElectricalNetworkRuntime first = new(
            topology,
            new FixedGridSystemProvider(building.Grid),
            clock,
            items,
            physicalFuel,
            new AutomationPowerDemandRegistry(firstStore),
            firstStore,
            firstBufferLifecycle,
            firstBufferLifecycle,
            firstBufferLifecycle);

        DungeonPowerInfrastructureSaveData pending = first.Capture();
        FacilityBufferCapacityProfile firstCapacity =
            firstBufferLifecycle.RequireSingleProfile();
        PowerNodeSaveData pendingNode = pending.nodes.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                nodeId,
                StringComparison.Ordinal));
        string operationId = ElectricalNetworkRuntime.FormatFuelOperationId(
            nodeId,
            1);
        Require((PowerFuelCommitPhase)pendingNode.pendingFuel.phase
                == PowerFuelCommitPhase.OutcomePublished
                && pendingNode.nextFuelOperationSequence == 1
                && Mathf.Approximately(
                    pendingNode.fuelSeconds,
                    secondsPerFuel)
                && pendingNode.pendingFuel.quantity == 1
                && pendingNode.pendingFuel.inputMassGrams
                    == items.MassQuery.GetDefinitionUnitMass(
                        (ItemDefinitionId)fuelItemId).Value
                && physicalFuel.TryGetPending(operationId, out _)
                && CountFuel(items, destinationId, fuelItemId)
                    == beforeQuantity - 1,
            "Fuel acknowledgement fault did not retain one debit and its published-time outbox.");

        const int RestoredTopologySourceVersion = 97;
        IndustrialTopologySnapshot restoredSnapshot =
            IndustrialInfrastructureTopologyBuilder.Build(
                RestoredTopologySourceVersion,
                new[] { building });
        FixedTopology restoredTopology = new(restoredSnapshot);
        RecordingFacilityBufferLifecycle restoredBufferLifecycle = new();
        DungeonRuntimeAggregateRootStore restoredStore = new();
        ElectricalNetworkRuntime restored = new(
            restoredTopology,
            new FixedGridSystemProvider(building.Grid),
            clock,
            items,
            physicalFuel,
            new AutomationPowerDemandRegistry(restoredStore),
            restoredStore,
            restoredBufferLifecycle,
            restoredBufferLifecycle,
            restoredBufferLifecycle);
        restored.Restore(restored.PrepareRestore(pending));
        FacilityBufferCapacityProfile restoredCapacity =
            restoredBufferLifecycle.RequireSingleProfile();
        Require(
            snapshot.SourceVersion != restoredSnapshot.SourceVersion
            && firstCapacity.CapacityRevision
                == ElectricalNetworkRuntime.FuelBufferCapacitySchemaRevision
            && restoredCapacity.CapacityRevision
                == ElectricalNetworkRuntime.FuelBufferCapacitySchemaRevision
            && firstCapacity.CapacityRevision
                == restoredCapacity.CapacityRevision
            && firstCapacity.MaxMassGrams == restoredCapacity.MaxMassGrams
            && string.Equals(
                firstCapacity.DestinationId,
                restoredCapacity.DestinationId,
                StringComparison.Ordinal),
            "Power fuel-buffer capacity revision changed with the topology epoch.");
        PowerNodeSaveData recoveredNode = restored.Capture().nodes.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                nodeId,
                StringComparison.Ordinal));
        Require((PowerFuelCommitPhase)recoveredNode.pendingFuel.phase
                == PowerFuelCommitPhase.None
                && recoveredNode.nextFuelOperationSequence == 2
                && Mathf.Approximately(
                    recoveredNode.fuelSeconds,
                    secondsPerFuel)
                && !physicalFuel.TryGetPending(operationId, out _)
                && CountFuel(items, destinationId, fuelItemId)
                    == beforeQuantity - 1,
            "Fuel restore did not finish acknowledgement-only recovery.");

        clock.Advance(10f);
        restored.Tick();
        PowerNodeSaveData afterTick = restored.Capture().nodes.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                nodeId,
                StringComparison.Ordinal));
        Require(Mathf.Approximately(
                    afterTick.fuelSeconds,
                    secondsPerFuel - 10f)
                && afterTick.nextFuelOperationSequence == 2
                && CountFuel(items, destinationId, fuelItemId)
                    == beforeQuantity - 1,
            "Fuel time did not decrease after recovery or a second fuel lot was consumed.");
        topology.Replace(IndustrialInfrastructureTopologyBuilder.Build(
            snapshot.SourceVersion + 1,
            Array.Empty<BuildableObject>()));
        clock.Advance(0.25f);
        first.Tick();
        Require(firstBufferLifecycle.ReleaseCount == 1
                && string.Equals(
                    firstBufferLifecycle.LastReleasedDestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && firstBufferLifecycle.CaptureClaims().Count == 0,
            "Retired generator did not terminally release its fuel destination before authority removal.");
        return "fuel-debit=1; outcome=1; ack-recovery=1; second-debit=0; "
            + "capacity-revision=stable; terminal-close=1";
    }

    private static int CountFuel(
        IWorldItemStackRuntime items,
        string destinationId,
        string fuelItemId) => items.GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, fuelItemId, StringComparison.Ordinal)
            && string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedTopology : IIndustrialInfrastructureTopologyRuntime
    {
        internal FixedTopology(IndustrialTopologySnapshot current) =>
            Current = current;

        public IndustrialTopologySnapshot Current { get; private set; }
        internal void Replace(IndustrialTopologySnapshot current) =>
            Current = current ?? throw new ArgumentNullException(nameof(current));
        public void MarkDirty()
        {
        }
    }

    private sealed class FixedGridSystemProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        internal FixedGridSystemProvider(Grid grid)
        {
            this.grid = grid
                ?? throw new ArgumentNullException(nameof(grid));
        }

        public GridSystemManager Manager => throw new NotSupportedException();
        public Grid Grid => grid;
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid value)
        {
            value = grid;
            return true;
        }
    }

    private sealed class ProbeClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;

        internal void Advance(float seconds)
        {
            DeltaTime = Mathf.Max(0f, seconds);
            Time += DeltaTime;
            FrameCount++;
        }
    }

    private sealed class RecordingFacilityBufferLifecycle :
        IFacilityBufferDestinationLifecycleCommand,
        IFacilityBufferDestinationClaimQuery,
        IFacilityBufferDestinationReleaseService
    {
        private FacilityBufferDestinationClaim[] claims =
            Array.Empty<FacilityBufferDestinationClaim>();
        private FacilityBufferCapacityProfile[] profiles =
            Array.Empty<FacilityBufferCapacityProfile>();

        internal int ReleaseCount { get; private set; }
        internal string LastReleasedDestinationId { get; private set; } =
            string.Empty;

        public long Revision => 1L;

        public bool TryGetClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = claims.SingleOrDefault(value => string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && value.DropPosition == dropPosition);
            return claim != null;
        }

        public System.Collections.Generic.IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureClaims() => claims;

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            ReleaseCount++;
            LastReleasedDestinationId = destinationId;
            releasedQuantity = 0;
            failureReason = string.Empty;
            return true;
        }

        internal FacilityBufferCapacityProfile RequireSingleProfile()
        {
            Require(profiles.Length == 1,
                "Power fuel probe did not publish exactly one capacity profile.");
            return profiles[0];
        }

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            System.Collections.Generic.IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            System.Collections.Generic.IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            failureReason = string.Empty;
            claims = (desiredClaims
                    ?? Array.Empty<FacilityBufferDestinationClaim>())
                .ToArray();
            profiles = (desiredProfiles
                    ?? Array.Empty<FacilityBufferCapacityProfile>())
                .ToArray();
            return true;
        }
    }
}
#endif
