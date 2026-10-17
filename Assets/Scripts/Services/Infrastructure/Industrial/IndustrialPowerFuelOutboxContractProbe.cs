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
    public static string RunBreakerTrip(
        BuildableObject building,
        IWorldItemStackRuntime items,
        IPhysicalFacilityItemSinkGateway physicalFuel,
        Action<string, string> observe = null)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (physicalFuel == null)
            throw new ArgumentNullException(nameof(physicalFuel));

        string nodeId = IndustrialInfrastructureIdentity.GetNodeId(building);
        IndustrialTopologySnapshot snapshot =
            IndustrialInfrastructureTopologyBuilder.Build(
                1,
                new[] { building });
        Require(snapshot.Nodes.ContainsKey(nodeId),
            "Power breaker probe building was not captured by industrial topology.");
        FixedTopology topology = new(snapshot);
        ProbeClock clock = new();
        RecordingFacilityBufferLifecycle buffers = new();
        DungeonRuntimeAggregateRootStore firstStore = new();
        using IndustrialTransitionObservationFixture outcomes = new(
            firstStore,
            clock,
            "run:power-breaker-probe",
            observe)
        {
            RejectNextPrepare = true
        };
        ElectricalNetworkRuntime first = new(
            topology,
            new FixedGridSystemProvider(building.Grid),
            new RestoreWorldCandidateIndex(),
            clock,
            items,
            physicalFuel,
            new AutomationPowerDemandRegistry(firstStore),
            firstStore,
            buffers,
            buffers,
            buffers,
            NeutralSeasonalEventQuery.Instance,
            null,
            outcomes);

        clock.Advance(2f);
        first.Tick();
        PowerNodeSaveData afterRejectedPrepare = first.Capture().nodes.Single(
            value => string.Equals(
                value.buildingInstanceId,
                nodeId,
                StringComparison.Ordinal));
        Require(!afterRejectedPrepare.breakerTripped
                && outcomes.Count(
                    InfrastructureCommandOutcomeKind.PowerBreakerTripped) == 0,
            "Rejected breaker outcome preparation changed the breaker or hid a record.");
        observe?.Invoke("rejected-owner", JsonUtility.ToJson(afterRejectedPrepare));

        clock.Advance(0.25f);
        first.Tick();
        DungeonPowerInfrastructureSaveData trippedSave = first.Capture();
        PowerNodeSaveData tripped = trippedSave.nodes.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                nodeId,
                StringComparison.Ordinal));
        Require(tripped.breakerTripped
                && outcomes.Count(
                    InfrastructureCommandOutcomeKind.PowerBreakerTripped) == 1,
            "Breaker threshold crossing was not emitted exactly once.");
        InfrastructureCommandOutcomeSource trip = outcomes.Single(
            InfrastructureCommandOutcomeKind.PowerBreakerTripped);
        Require(trip.BeforeValue.Contains("breaker=false", StringComparison.Ordinal)
                && trip.BeforeValue.Contains("loadRatio=3", StringComparison.Ordinal)
                && trip.AfterValue.Contains("breaker=true", StringComparison.Ordinal)
                && trip.AfterValue.Contains("tripHeat=61", StringComparison.Ordinal)
                && trip.AfterValue.Contains("overloadTolerance=1.1", StringComparison.Ordinal),
            "Breaker trip outcome lost its exact threshold transition facts.");
        observe?.Invoke("tripped-owner", JsonUtility.ToJson(trippedSave));

        clock.Advance(0.25f);
        first.Tick();
        Require(outcomes.Count(
                    InfrastructureCommandOutcomeKind.PowerBreakerTripped) == 1,
            "Steady tripped state emitted a duplicate autonomous outcome.");

        DungeonPowerInfrastructureSaveData roundTrip =
            JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(
                JsonUtility.ToJson(trippedSave));
        RecordingFacilityBufferLifecycle restoredBuffers = new();
        DungeonRuntimeAggregateRootStore restoredStore = new();
        ElectricalNetworkRuntime restored = new(
            topology,
            new FixedGridSystemProvider(building.Grid),
            new RestoreWorldCandidateIndex(),
            clock,
            items,
            physicalFuel,
            new AutomationPowerDemandRegistry(restoredStore),
            restoredStore,
            restoredBuffers,
            restoredBuffers,
            restoredBuffers,
            NeutralSeasonalEventQuery.Instance,
            null,
            outcomes);
        restored.Restore(restored.PrepareRestore(roundTrip));
        clock.Advance(3f);
        restored.Tick();
        Require(outcomes.Count(
                    InfrastructureCommandOutcomeKind.PowerBreakerTripped) == 1,
            "Restoring a tripped breaker emitted a duplicate autonomous outcome.");
        observe?.Invoke("restored-owner", JsonUtility.ToJson(restored.Capture()));

        InfrastructureCommandResult reset = restored.ResetBreaker(building);
        Require(reset.Succeeded
                && outcomes.Count(
                    InfrastructureCommandOutcomeKind.PowerBreakerReset) == 1
                && outcomes.Count(
                    InfrastructureCommandOutcomeKind.PowerBreakerTripped) == 1,
            "Manual breaker reset was not kept as a separate command outcome.");
        observe?.Invoke("reset-owner", JsonUtility.ToJson(restored.Capture()));
        return "prepare-rejection=visible;trip-edge=1;steady-duplicate=0;"
            + "save-duplicate=0;manual-reset=separate";
    }

    public static string Run(
        BuildableObject building,
        IWorldItemStackRuntime items,
        IPhysicalFacilityItemSinkGateway physicalFuel,
        string fuelItemId,
        float secondsPerFuel,
        Action<string, string> observe = null)
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
        observe?.Invoke("before-quantity", beforeQuantity.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
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
        using IndustrialTransitionObservationFixture outcomes = new(
            firstStore,
            clock,
            "run:power-fuel-probe",
            observe);
        ElectricalNetworkRuntime first = new(
            topology,
            new FixedGridSystemProvider(building.Grid),
            new RestoreWorldCandidateIndex(),
            clock,
            items,
            physicalFuel,
            new AutomationPowerDemandRegistry(firstStore),
            firstStore,
            firstBufferLifecycle,
            firstBufferLifecycle,
            firstBufferLifecycle,
            NeutralSeasonalEventQuery.Instance,
            null,
            outcomes);

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
        observe?.Invoke("pending-owner", JsonUtility.ToJson(pending));
        observe?.Invoke("pending-quantity", CountFuel(items, destinationId, fuelItemId)
            .ToString(System.Globalization.CultureInfo.InvariantCulture));

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
            new RestoreWorldCandidateIndex(),
            clock,
            items,
            physicalFuel,
            new AutomationPowerDemandRegistry(restoredStore),
            restoredStore,
            restoredBufferLifecycle,
            restoredBufferLifecycle,
            restoredBufferLifecycle,
            NeutralSeasonalEventQuery.Instance,
            null,
            outcomes);
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
        DungeonPowerInfrastructureSaveData recoveredSave = restored.Capture();
        PowerNodeSaveData recoveredNode = recoveredSave.nodes.Single(value =>
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
        observe?.Invoke("restored-owner", JsonUtility.ToJson(recoveredSave));
        observe?.Invoke("restored-quantity", CountFuel(items, destinationId, fuelItemId)
            .ToString(System.Globalization.CultureInfo.InvariantCulture));

        clock.Advance(10f);
        restored.Tick();
        DungeonPowerInfrastructureSaveData afterTickSave = restored.Capture();
        PowerNodeSaveData afterTick = afterTickSave.nodes.Single(value =>
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
        observe?.Invoke("after-tick-owner", JsonUtility.ToJson(afterTickSave));
        restoredTopology.Replace(IndustrialInfrastructureTopologyBuilder.Build(
            restoredSnapshot.SourceVersion + 1,
            Array.Empty<BuildableObject>()));
        clock.Advance(0.25f);
        restored.Tick();
        Require(restoredBufferLifecycle.ReleaseCount == 1
                && string.Equals(
                    restoredBufferLifecycle.LastReleasedDestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && restoredBufferLifecycle.CaptureClaims().Count == 0,
            "Retired generator did not terminally release its fuel destination before authority removal.");
        observe?.Invoke("terminal-release-count", restoredBufferLifecycle.ReleaseCount
            .ToString(System.Globalization.CultureInfo.InvariantCulture));
        observe?.Invoke("terminal-claim-count", restoredBufferLifecycle.CaptureClaims().Count
            .ToString(System.Globalization.CultureInfo.InvariantCulture));
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

    private sealed class ProbeClock : IGameClock, IGameCalendar
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        public int Day => 1;
        public int Hour => 0;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => Time;
        public TimeOfDay TimeOfDay => TimeOfDay.Night;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour) =>
            throw new NotSupportedException();

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
