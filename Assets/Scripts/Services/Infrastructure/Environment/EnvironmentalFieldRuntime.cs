using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;
using EnvironmentalFieldAggregateState =
    DungeonStory.Environment.EnvironmentalFieldAggregateState;
using EnvironmentalFieldAggregateStateStore =
    DungeonStory.Environment.EnvironmentalFieldAggregateStateStore;
using EnvironmentalFieldRestoreCandidate =
    DungeonStory.Environment.EnvironmentalFieldRestoreCandidate;

public sealed class EnvironmentalFieldRuntimeApplicationAdapter :
    IEnvironmentalFieldQuery,
    IEnvironmentalFieldCommand,
    IEnvironmentalFieldPersistence,
    ITickable,
    IDungeonRestoreTransactionParticipant
{
    private static readonly ProfilerMarker TickMarker =
        new ProfilerMarker("Environment.Field.Tick");
    private readonly IGridSystemProvider gridProvider;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly ISurvivalEnvironmentQuery survivalEnvironment;
    private readonly IPowerInfrastructureQuery power;
    private readonly IGameClock clock;
    private readonly IGameCalendar calendar;
    private readonly EnvironmentalFieldAggregateStateStore stateStore;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IDomainFactPublisher factPublisher;
    private readonly WeakReference<Grid> gridReference = new(null);
    private readonly List<EnvironmentalFieldSourceDescriptor> sources = new();
    private readonly List<(Door Door, int Revision)> operationDoors = new();
    private bool restoreTransactionActive;
    private Grid previousGrid;
    private EnvironmentalFieldSourceDescriptor[] previousSources;
    private (Door Door, int Revision)[] previousOperationDoors;

    private EnvironmentalFieldAggregateState State => stateStore.Current;
    private Grid grid => gridReference.TryGetTarget(out Grid current)
        ? current
        : null;
    private float[] temperature => State.Temperature;
    private float[] nextTemperature => State.NextTemperature;
    private float[] air => State.Air;
    private float[] nextAir => State.NextAir;
    private float[] light => State.Light;
    private float[] nextLight => State.NextLight;
    private bool[] barriers => State.Barriers;
    private bool[] doors => State.Doors;
    private float[] ductExchange => State.DuctExchange;
    private bool[] exterior => State.Exterior;
    private Dictionary<BuildingInstanceId, float> targetOverrides =>
        State.TargetOverrides;

    public EnvironmentalFieldRuntimeApplicationAdapter(
        IGridSystemProvider gridProvider,
        IBuildingWorldQuery buildingWorld,
        ISurvivalEnvironmentQuery survivalEnvironment,
        IPowerInfrastructureQuery power,
        IGameClock clock,
        IGameCalendar calendar,
        EnvironmentalFieldAggregateStateStore stateStore,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IDomainFactPublisher factPublisher)
    {
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.survivalEnvironment = survivalEnvironment
            ?? throw new ArgumentNullException(nameof(survivalEnvironment));
        this.power = power
            ?? throw new ArgumentNullException(nameof(power));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.factPublisher = factPublisher
            ?? throw new ArgumentNullException(nameof(factPublisher));
    }

    public int Version => State.Version;
    public bool IsInitialized => grid != null && temperature != null;

    // Numeric field state is staged in the aggregate root, but these Unity
    // object bindings live outside it. Roll them back with the same transaction
    // so the next Tick does not mistake the old world for a new empty grid.
    public string ParticipantId => "environment.field.bindings";

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive)
            throw new InvalidOperationException("Environmental field binding restore is already active.");
        previousGrid = grid;
        previousSources = sources.ToArray();
        previousOperationDoors = operationDoors.ToArray();
        restoreTransactionActive = true;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive)
            throw new InvalidOperationException("Environmental field binding restore is not active.");
    }

    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();

    public void DiscardRestoreCandidate()
    {
        if (!restoreTransactionActive) return;
        gridReference.SetTarget(previousGrid);
        sources.Clear();
        sources.AddRange(previousSources);
        operationDoors.Clear();
        operationDoors.AddRange(previousOperationDoors);
        ClearRestoreBindings();
    }

    public void CompleteRestoreCandidate() => ClearRestoreBindings();

    private void ClearRestoreBindings()
    {
        restoreTransactionActive = false;
        previousGrid = null;
        previousSources = null;
        previousOperationDoors = null;
    }

    public void Tick()
    {
        AdvanceControlOutcomes();
        if (!gridProvider.TryGetGrid(out Grid loadedGrid))
        {
            return;
        }

        EnsureInitialized(loadedGrid);
        if (clock.IsPaused)
        {
            return;
        }

        State.Accumulator += Mathf.Max(0f, clock.DeltaTime);
        while (State.Accumulator
               >= DungeonStory.Environment.EnvironmentalFieldSimulationRules
                   .TickInterval)
        {
            State.Accumulator -=
                DungeonStory.Environment.EnvironmentalFieldSimulationRules
                    .TickInterval;
            using (TickMarker.Auto())
            {
                Step(
                    DungeonStory.Environment.EnvironmentalFieldSimulationRules
                        .TickInterval);
            }
        }
    }

    public bool TryGetCell(
        Vector2Int position,
        out EnvironmentalCellSnapshot snapshot)
    {
        if (!IsInitialized
            || !grid.TryGetCellIndex(position, out int index))
        {
            snapshot = default;
            return false;
        }

        snapshot = new EnvironmentalCellSnapshot(
            position,
            temperature[index],
            air[index],
            light[index]);
        return true;
    }

    public bool TryGetAverage(
        IReadOnlyList<Vector2Int> positions,
        out EnvironmentalCellSnapshot snapshot)
    {
        if (!IsInitialized || positions == null || positions.Count == 0)
        {
            snapshot = default;
            return false;
        }

        float temperatureTotal = 0f;
        float airTotal = 0f;
        float lightTotal = 0f;
        int count = 0;
        Vector2Int anchor = positions[0];
        for (int i = 0; i < positions.Count; i++)
        {
            if (!grid.TryGetCellIndex(positions[i], out int index))
            {
                continue;
            }

            temperatureTotal += temperature[index];
            airTotal += air[index];
            lightTotal += light[index];
            count++;
        }

        if (count == 0)
        {
            snapshot = default;
            return false;
        }

        snapshot = new EnvironmentalCellSnapshot(
            anchor,
            temperatureTotal / count,
            airTotal / count,
            lightTotal / count);
        return true;
    }

    public float GetFoodSpoilageMultiplier(Vector2Int position)
    {
        if (!TryGetCell(position, out EnvironmentalCellSnapshot snapshot))
        {
            throw new InvalidOperationException(
                $"Environmental cell {position} is unavailable.");
        }

        return EnvironmentalThresholdRules.GetFoodSpoilageMultiplier(
            snapshot.TemperatureC);
    }

    public bool IsOrganPreservationSafe(Vector2Int position)
    {
        return TryGetCell(position, out EnvironmentalCellSnapshot snapshot)
            && EnvironmentalThresholdRules.IsOrganPreservationSafe(
                snapshot.TemperatureC);
    }

    public bool TryGetTargetTemperature(
        Vector2Int buildingPosition,
        out float targetTemperatureC)
    {
        EnvironmentalFieldSourceDescriptor source = FindConfigurableThermalSource(
            buildingPosition);
        if (source?.Thermal == null)
        {
            targetTemperatureC = 0f;
            return false;
        }

        if (targetOverrides.TryGetValue(
            source.BuildingId,
            out targetTemperatureC))
        {
            return true;
        }

        targetTemperatureC = source.Thermal.targetTemperatureC;
        return true;
    }

    public bool TrySetTargetTemperature(
        Vector2Int buildingPosition,
        float targetTemperatureC,
        out DomainFailure failure)
    {
        EnvironmentalFieldSourceDescriptor source = FindConfigurableThermalSource(
            buildingPosition);
        if (source?.Thermal == null)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentThermostatUnsupported,
                buildingPosition.x.ToString(CultureInfo.InvariantCulture),
                buildingPosition.y.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        BuildingThermalEmitterAbility emitter = source.Thermal;
        float minimum = Mathf.Min(
            emitter.minimumTargetTemperatureC,
            emitter.maximumTargetTemperatureC);
        float maximum = Mathf.Max(
            emitter.minimumTargetTemperatureC,
            emitter.maximumTargetTemperatureC);
        if (State.ControlOutcomes.TryGetValue(
                source.BuildingId,
                out EnvironmentalControlOutcomeSaveData prior)
            && !TryAdvanceControlOutcome(source.BuildingId, prior))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentOutcomeUnavailable,
                source.BuildingId.Value,
                "control-outcome-pending");
            return false;
        }
        float before = targetOverrides.TryGetValue(source.BuildingId, out float existing)
            ? existing
            : emitter.targetTemperatureC;
        float applied = DungeonStory.Environment.EnvironmentalFieldSimulationRules
            .ClampThermostatTarget(targetTemperatureC, minimum, maximum);
        if (Mathf.Approximately(before, applied))
        {
            failure = DomainFailure.None;
            return true;
        }
        int sequence = State.NextControlOutcomeSequence;
        long ownerRevision = checked((long)sequence + 1L);
        var pending = new EnvironmentalControlOutcomeSaveData
        {
            operationId = "environment-control:" + source.BuildingId.Value + ":"
                + sequence.ToString("D6", CultureInfo.InvariantCulture),
            buildingInstanceId = source.BuildingId.Value,
            buildingDisplayName = RequireBuildingDisplayName(source.Building),
            commandSource = "environmental-field-command",
            x = buildingPosition.x,
            y = buildingPosition.y,
            requestedTargetTemperatureC = targetTemperatureC,
            appliedTargetTemperatureC = applied,
            minimumTargetTemperatureC = minimum,
            maximumTargetTemperatureC = maximum,
            targetBeforeTemperatureC = before,
            targetAfterTemperatureC = applied,
            absoluteDay = Mathf.Max(0, calendar.Day),
            ownerRevision = ownerRevision
        };
        var fact = new EnvironmentalControlFact(pending);
        if (!factPublisher.TryReserve(
                fact,
                out DomainFactLease lease,
                out string reserveFailure))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentOutcomeUnavailable,
                source.BuildingId.Value,
                reserveFailure);
            return false;
        }
        try
        {
            targetOverrides[source.BuildingId] = applied;
            State.ControlOutcomes.Add(source.BuildingId, pending);
            State.NextControlOutcomeSequence = checked(sequence + 1);
            failure = DomainFailure.None;
            Touch();
        }
        catch
        {
            factPublisher.Cancel(lease);
            throw;
        }
        DomainFactDelivery delivery = factPublisher.Publish(lease, fact);
        if (delivery.Completed)
            State.ControlOutcomes.Remove(source.BuildingId);
        return true;
    }

    public void MarkTopologyDirty()
    {
        State.TopologyDirty = true;
    }

    public DungeonEnvironmentalFieldSaveData Capture()
    {
        DungeonEnvironmentalFieldSaveData result =
            new DungeonEnvironmentalFieldSaveData();
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "Environmental field must be initialized before capture.");
        }

        result.width = grid.width;
        result.height = grid.height;
        float outdoorTemperature = GetOutdoorTemperature();
        float exteriorAmbientLight = GetExteriorAmbientLight();
        int count = grid.width * grid.height;
        for (int index = 0; index < count; index++)
        {
            Vector2Int position = grid.GetPositionFromCellIndex(index);
            float baseLight = GetBaseLight(
                exterior[index],
                exteriorAmbientLight);
            if (Mathf.Abs(temperature[index] - outdoorTemperature) < 0.05f
                && Mathf.Abs(air[index] - 100f) < 0.05f
                && Mathf.Abs(light[index] - baseLight) < 0.05f)
            {
                continue;
            }

            result.cells.Add(new EnvironmentalCellSaveData
            {
                x = position.x,
                y = position.y,
                temperatureC = temperature[index],
                airQuality = air[index],
                lightLevel = light[index]
            });
        }

        foreach (KeyValuePair<BuildingInstanceId, float> thermostat
                 in targetOverrides.OrderBy(
                     pair => pair.Key.Value,
                     StringComparer.Ordinal))
        {
            result.thermostats.Add(new EnvironmentalThermostatSaveData
            {
                buildingInstanceId = thermostat.Key.Value,
                targetTemperatureC = thermostat.Value
            });
        }

        result.nextControlOutcomeSequence = State.NextControlOutcomeSequence;
        result.controlOutcomes = State.ControlOutcomes.Values
            .OrderBy(value => value.buildingInstanceId, StringComparer.Ordinal)
            .Select(CloneControlOutcome)
            .ToList();

        return result;
    }

    public EnvironmentalFieldRestoreCandidate PrepareRestore(
        DungeonEnvironmentalFieldSaveData saveData)
    {
        DungeonEnvironmentalFieldSaveData source = saveData
            ?? throw new InvalidOperationException(
                "Environmental-field payload is null.");
        if (source.version != DungeonEnvironmentalFieldSaveData.CurrentVersion
            || source.cells == null
            || source.thermostats == null
            || source.controlOutcomes == null
            || source.nextControlOutcomeSequence < 0)
        {
            throw new InvalidOperationException(
                "Environmental-field payload is incomplete or has an unsupported version.");
        }
        if (source.width <= 0 || source.height <= 0)
        {
            throw new InvalidOperationException(
                $"Environmental-field dimensions {source.width}x{source.height} are invalid.");
        }
        if (restoreWorldCandidates.TryGetGrid(out Grid candidateGrid)
            && (source.width != candidateGrid.width
                || source.height != candidateGrid.height))
        {
            throw new InvalidOperationException(
                $"Environmental-field dimensions {source.width}x{source.height} do not match the staged grid {candidateGrid.width}x{candidateGrid.height}.");
        }

        EnvironmentalFieldRestoreCandidate candidate =
            DungeonStory.Environment.EnvironmentalFieldRestoreRules.Prepare(
                source.width,
                source.height,
                source.cells.Select(cell =>
                {
                    if (cell == null)
                    {
                        throw new InvalidOperationException(
                            "Environmental-field payload contains a null cell record.");
                    }
                    return new DungeonStory.Environment.EnvironmentalCellSnapshot(
                        new DungeonStory.Environment.EnvironmentalCellAddress(
                            cell.x,
                            cell.y),
                        cell.temperatureC,
                        cell.airQuality,
                        cell.lightLevel);
                }).ToArray(),
                source.thermostats.Select(thermostat =>
                {
                    if (thermostat == null)
                    {
                        throw new InvalidOperationException(
                            "Environmental-field payload contains a null thermostat record.");
                    }
                    return new DungeonStory.Environment.EnvironmentalThermostatRecord(
                        (BuildingInstanceId)thermostat.buildingInstanceId,
                        thermostat.targetTemperatureC);
                }).ToArray(),
                source.nextControlOutcomeSequence,
                source.controlOutcomes.Select(CloneControlOutcome).ToArray());

        int previousCellIndex = -1;
        foreach (EnvironmentalCellSaveData cell in source.cells)
        {
            if (cell == null
                || !IsFiniteInRange(cell.temperatureC, -50f, 80f)
                || !IsFiniteInRange(cell.airQuality, 0f, 100f)
                || !IsFiniteInRange(cell.lightLevel, 0f, 100f))
            {
                throw new InvalidOperationException(
                    "Environmental-field payload contains a null or invalid cell record.");
            }
            Vector2Int position = new Vector2Int(cell.x, cell.y);
            if (position.x < 0
                || position.x >= source.width
                || position.y < 0
                || position.y >= source.height)
            {
                throw new InvalidOperationException(
                    $"Environmental-field cell {position} is outside the saved dimensions.");
            }
            int index = checked(position.y * source.width + position.x);
            if (index <= previousCellIndex)
            {
                throw new InvalidOperationException(
                    $"Environmental-field cell {position} is duplicated or unordered.");
            }
            previousCellIndex = index;
        }

        string previousBuildingId = null;
        foreach (EnvironmentalThermostatSaveData thermostat in source.thermostats)
        {
            string rawId = thermostat?.buildingInstanceId ?? string.Empty;
            BuildingInstanceId buildingId = (BuildingInstanceId)rawId;
            if (thermostat == null
                || !buildingId.IsValid
                || !string.Equals(buildingId.Value, rawId, StringComparison.Ordinal)
                || previousBuildingId != null
                    && string.CompareOrdinal(previousBuildingId, rawId) >= 0
                || !IsFiniteInRange(
                    thermostat.targetTemperatureC,
                    -50f,
                    80f))
            {
                throw new InvalidOperationException(
                    "Environmental-field thermostats require canonical, unique, sorted building IDs and finite targets.");
            }
            BuildingThermalEmitterAbility emitter =
                FindConfigurableThermalEmitter(buildingId);
            if (emitter == null)
            {
                throw new InvalidOperationException(
                    $"Environmental-field thermostat owner '{rawId}' is missing or not configurable.");
            }
            float minimum = Mathf.Min(
                emitter.minimumTargetTemperatureC,
                emitter.maximumTargetTemperatureC);
            float maximum = Mathf.Max(
                emitter.minimumTargetTemperatureC,
                emitter.maximumTargetTemperatureC);
            if (thermostat.targetTemperatureC < minimum
                || thermostat.targetTemperatureC > maximum)
            {
                throw new InvalidOperationException(
                    $"Environmental-field thermostat '{rawId}' target is outside its authored range.");
            }
            previousBuildingId = rawId;
        }

        previousBuildingId = null;
        foreach (EnvironmentalControlOutcomeSaveData outcome
                 in source.controlOutcomes)
        {
            string rawId = outcome?.buildingInstanceId ?? string.Empty;
            if (!IsValidControlOutcome(outcome)
                || previousBuildingId != null
                    && string.CompareOrdinal(previousBuildingId, rawId) >= 0)
            {
                throw new InvalidOperationException(
                    "Environmental-field control outcomes are invalid, duplicate, or unordered.");
            }
            previousBuildingId = rawId;
        }

        return candidate;
    }

    public void Restore(EnvironmentalFieldRestoreCandidate candidate)
    {
        EnvironmentalFieldRestoreCandidate source = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        if (!restoreWorldCandidates.TryGetGrid(out Grid loadedGrid)
            && !gridProvider.TryGetGrid(out loadedGrid))
        {
            throw new InvalidOperationException(
                "Environmental-field restore requires a loaded grid.");
        }
        if (source.Width != loadedGrid.width
            || source.Height != loadedGrid.height)
        {
            throw new InvalidOperationException(
                $"Environmental-field dimensions {source.Width}x{source.Height} do not match the loaded grid {loadedGrid.width}x{loadedGrid.height}.");
        }

        EnvironmentalFieldAggregateState restored =
            CreateInitializedState(loadedGrid, Version + 1);
        foreach (DungeonStory.Environment.EnvironmentalCellSnapshot cell
                 in source.Cells)
        {
            Vector2Int position = new(cell.Address.X, cell.Address.Y);
            if (!loadedGrid.TryGetCellIndex(position, out int index))
            {
                throw new InvalidOperationException(
                    $"Environmental-field cell {position} is outside the loaded grid.");
            }

            restored.Temperature[index] = cell.TemperatureC;
            restored.Air[index] = cell.AirQuality;
            restored.Light[index] = cell.LightLevel;
        }

        foreach (DungeonStory.Environment.EnvironmentalThermostatRecord thermostat
                 in source.Thermostats)
        {
            restored.TargetOverrides.Add(
                thermostat.BuildingId,
                thermostat.TargetTemperatureC);
        }
        restored.NextControlOutcomeSequence = source.NextControlOutcomeSequence;
        foreach (EnvironmentalControlOutcomeSaveData pending
                 in source.ControlOutcomes)
        {
            var buildingId = (BuildingInstanceId)pending.buildingInstanceId;
            restored.ControlOutcomes.Add(
                buildingId,
                CloneControlOutcome(pending));
        }

        Array.Copy(
            restored.Temperature,
            restored.NextTemperature,
            restored.Temperature.Length);
        Array.Copy(restored.Air, restored.NextAir, restored.Air.Length);
        Array.Copy(restored.Light, restored.NextLight, restored.Light.Length);
        gridReference.SetTarget(loadedGrid);
        sources.Clear();
        stateStore.Replace(restored);
    }

    public void Reset()
    {
        gridReference.SetTarget(null);
        sources.Clear();
        stateStore.Replace(new EnvironmentalFieldAggregateState
        {
            Version = Version + 1
        });
    }

    private void AdvanceControlOutcomes()
    {
        foreach (KeyValuePair<BuildingInstanceId, EnvironmentalControlOutcomeSaveData>
                 pair in State.ControlOutcomes.ToArray())
            TryAdvanceControlOutcome(pair.Key, pair.Value);
    }

    private bool TryAdvanceControlOutcome(
        BuildingInstanceId buildingId,
        EnvironmentalControlOutcomeSaveData pending)
    {
        var fact = new EnvironmentalControlFact(pending);
        DomainFactDelivery delivery = factPublisher.Replay(fact);
        if (!delivery.Completed)
            return false;
        State.ControlOutcomes.Remove(buildingId);
        return true;
    }

    private static string RequireBuildingDisplayName(BuildableObject building)
    {
        if (building == null)
        {
            throw new InvalidOperationException(
                "Environmental control requires a live building instance.");
        }

        string value = building.BuildingData == null
            ? string.Empty
            : building.BuildingData.objectName?.Trim();
        if (string.IsNullOrEmpty(value))
            value = building.name?.Trim();
        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException(
                "Environmental control requires an immutable building display name.")
            : value;
    }

    private static EnvironmentalControlOutcomeSaveData CloneControlOutcome(
        EnvironmentalControlOutcomeSaveData source) => new()
    {
        schemaVersion = source.schemaVersion,
        operationId = source.operationId,
        buildingInstanceId = source.buildingInstanceId,
        buildingDisplayName = source.buildingDisplayName,
        commandSource = source.commandSource,
        x = source.x,
        y = source.y,
        requestedTargetTemperatureC = source.requestedTargetTemperatureC,
        appliedTargetTemperatureC = source.appliedTargetTemperatureC,
        minimumTargetTemperatureC = source.minimumTargetTemperatureC,
        maximumTargetTemperatureC = source.maximumTargetTemperatureC,
        targetBeforeTemperatureC = source.targetBeforeTemperatureC,
        targetAfterTemperatureC = source.targetAfterTemperatureC,
        absoluteDay = source.absoluteDay,
        ownerRevision = source.ownerRevision
    };

    private static bool IsValidControlOutcome(
        EnvironmentalControlOutcomeSaveData value) =>
        value != null
        && value.schemaVersion
            == EnvironmentalControlOutcomeSaveData.CurrentSchemaVersion
        && GameplayOutcomeStableIdSyntax.IsValid(value.operationId)
        && ((BuildingInstanceId)value.buildingInstanceId).IsValid
        && !string.IsNullOrWhiteSpace(value.buildingDisplayName)
        && GameplayOutcomeStableIdSyntax.IsValid(value.commandSource)
        && value.absoluteDay >= 0
        && value.ownerRevision > 0L
        && IsFiniteInRange(value.requestedTargetTemperatureC, -1000f, 1000f)
        && IsFiniteInRange(value.appliedTargetTemperatureC, -50f, 80f)
        && IsFiniteInRange(value.minimumTargetTemperatureC, -50f, 80f)
        && IsFiniteInRange(value.maximumTargetTemperatureC, -50f, 80f)
        && value.minimumTargetTemperatureC <= value.maximumTargetTemperatureC
        && value.appliedTargetTemperatureC >= value.minimumTargetTemperatureC
        && value.appliedTargetTemperatureC <= value.maximumTargetTemperatureC
        && Mathf.Approximately(
            value.appliedTargetTemperatureC,
            value.targetAfterTemperatureC)
        && !Mathf.Approximately(
            value.targetBeforeTemperatureC,
            value.targetAfterTemperatureC);

    private void EnsureInitialized(Grid loadedGrid)
    {
        if (ReferenceEquals(grid, loadedGrid)
            && temperature != null
            && temperature.Length == loadedGrid.width * loadedGrid.height)
        {
            RefreshTopologyIfNeeded();
            return;
        }

        gridReference.SetTarget(loadedGrid);
        sources.Clear();
        stateStore.Replace(CreateInitializedState(loadedGrid, Version + 1));
        RebuildTopology();
    }

    private EnvironmentalFieldAggregateState CreateInitializedState(
        Grid loadedGrid,
        int version)
    {
        int count = loadedGrid.width * loadedGrid.height;
        bool[] exteriorTopology = new bool[count];
        for (int index = 0; index < count; index++)
        {
            Vector2Int position = loadedGrid.GetPositionFromCellIndex(index);
            GridCell cell = loadedGrid.GetGridCell(position);
            exteriorTopology[index] = cell == null
                || cell.AreaType != GridCellAreaType.DungeonInterior;
        }
        return DungeonStory.Environment.EnvironmentalFieldSimulationRules
            .CreateInitialized(
                loadedGrid.width,
                loadedGrid.height,
                GetOutdoorTemperature(),
                exteriorTopology,
                GetExteriorAmbientLight(),
                version);
    }

    private void Step(float deltaTime)
    {
        RefreshTopologyIfNeeded();
        DungeonStory.Environment.EnvironmentalFieldSimulationRules
            .StepDiffusion(
                State,
                GetOutdoorTemperature(),
                GetExteriorAmbientLight(),
                deltaTime);
        ApplySources(deltaTime);
        DungeonStory.Environment.EnvironmentalFieldSimulationRules
            .CompleteStep(State);
    }

    private void ApplySources(float deltaTime)
    {
        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            EnvironmentalFieldSourceDescriptor source = sources[sourceIndex];
            bool active = source.Building != null && !source.Building.isDestroy
                && source.Building.isActiveAndEnabled && !source.Building.IsDetachedRestoreCandidate
                && source.Building.Grid == grid
                && !(source.Building.IsDamaged
                    && source.Building.Facility?.disabledWhenDamaged == true);
            bool powered = active && (!(source.RequiresPower || source.LightRequiresPower)
                || power.IsPowered(source.Building));
            bool fuelSupplied = !source.RequiresFuel
                || survivalEnvironment.HasFuelSupply(source.Building);
            bool emitsLight = active && (!source.LightRequiresPower || powered)
                && (!source.LightRequiresFuel || fuelSupplied);
            source.LightVisual?.SetEmissionEnabled(emitsLight);
            if (!active) continue;

            bool emitsThermal = source.Thermal != null
                && (!source.RequiresPower || powered)
                && (!source.ThermalRequiresFuel || fuelSupplied);
            bool runsLegacyHeating = source.LegacyTemperature != null
                && (!source.LegacyTemperatureRequiresFuel || fuelSupplied);
            float operatingSeconds = source.RequiresFuel
                ? Mathf.Min(
                    deltaTime,
                    survivalEnvironment.GetRemainingFuelGameSeconds(
                        source.Building))
                : deltaTime;
            if (emitsThermal)
            {
                ApplyThermalSource(source, operatingSeconds);
            }

            if (source.Air != null && (!source.RequiresPower || powered))
            {
                ApplyAirSource(source, deltaTime);
            }

            if (source.Light != null && emitsLight)
            {
                ApplyLightSource(source);
            }

            if (source.RequiresFuel
                && operatingSeconds > 0f
                && (emitsThermal || runsLegacyHeating || emitsLight))
            {
                source.Building.ConsumeFacilityFuelGameSeconds(operatingSeconds);
            }
        }
    }

    private void ApplyThermalSource(
        EnvironmentalFieldSourceDescriptor source,
        float deltaTime)
    {
        BuildingThermalEmitterAbility emitter = source.Thermal;
        float targetTemperatureC = targetOverrides.TryGetValue(
            source.BuildingId,
            out float configuredTarget)
                ? configuredTarget
                : emitter.targetTemperatureC;
        VisitRadius(
            source.Position,
            emitter.radius,
            (index, distance01) =>
            {
                nextTemperature[index] =
                    DungeonStory.Environment.EnvironmentalFieldSimulationRules
                        .ApplyThermalSource(
                            nextTemperature[index],
                            emitter.mode,
                            targetTemperatureC,
                            emitter.degreesPerSecond,
                            distance01,
                            deltaTime);
            });

        if (emitter.mode == ThermalEmitterMode.Cool)
        {
            Vector2Int exhaustPosition =
                source.Position + emitter.exhaustOffset;
            if (grid.TryGetCellIndex(exhaustPosition, out int exhaustIndex)
                && !barriers[exhaustIndex])
            {
                nextTemperature[exhaustIndex] =
                    DungeonStory.Environment.EnvironmentalFieldSimulationRules
                        .ApplyCoolerExhaust(
                            nextTemperature[exhaustIndex],
                            emitter.degreesPerSecond,
                            emitter.exhaustHeatMultiplier,
                            deltaTime);
            }
        }
    }

    private EnvironmentalFieldSourceDescriptor FindConfigurableThermalSource(
        Vector2Int buildingPosition)
    {
        if (IsInitialized)
        {
            RefreshTopologyIfNeeded();
        }

        return sources.FirstOrDefault(source =>
            source != null
            && source.Position == buildingPosition
            && source.Thermal?.playerConfigurable == true);
    }

    private BuildingThermalEmitterAbility FindConfigurableThermalEmitter(
        BuildingInstanceId buildingId)
    {
        IReadOnlyList<BuildableObject> buildings =
            buildingWorld.Buildings ?? Array.Empty<BuildableObject>();
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject building = buildings[index];
            if (building == null
                || building.isDestroy
                || !building.PersistentInstanceId.Equals(buildingId))
            {
                continue;
            }
            BuildingThermalEmitterAbility emitter =
                building.BuildingData
                    ?.GetAbility<BuildingThermalEmitterAbility>();
            return emitter?.playerConfigurable == true ? emitter : null;
        }
        return null;
    }

    private void ApplyAirSource(
        EnvironmentalFieldSourceDescriptor source,
        float deltaTime)
    {
        BuildingAirExchangeAbility exchange = source.Air;
        float target = exchange.exchangesWithOutside
            ? 100f
            : exchange.targetAirQuality;
        VisitRadius(
            source.Position,
            exchange.radius,
            (index, distance01) =>
            {
                nextAir[index] =
                    DungeonStory.Environment.EnvironmentalFieldSimulationRules
                        .ApplyAirSource(
                            nextAir[index],
                            target,
                            exchange.qualityPerSecond,
                            distance01,
                            deltaTime);
            });
    }

    private void ApplyLightSource(EnvironmentalFieldSourceDescriptor source)
    {
        int radius = Mathf.Max(1, Mathf.CeilToInt(source.Light.radius));
        VisitRadius(
            source.Position,
            radius,
            (index, distance01) =>
            {
                nextLight[index] =
                    DungeonStory.Environment.EnvironmentalFieldSimulationRules
                        .ApplyLightSource(
                            nextLight[index],
                            source.Light.intensity,
                            distance01);
            });
    }

    private void VisitRadius(
        Vector2Int center,
        int radius,
        Action<int, float> visitor)
    {
        int clampedRadius = Mathf.Max(0, radius);
        for (int y = -clampedRadius; y <= clampedRadius; y++)
        {
            for (int x = -clampedRadius; x <= clampedRadius; x++)
            {
                int distance = Mathf.Abs(x) + Mathf.Abs(y);
                if (distance > clampedRadius)
                {
                    continue;
                }

                Vector2Int position = center + new Vector2Int(x, y);
                if (!grid.TryGetCellIndex(position, out int index)
                    || barriers[index]
                    || !HasLineOfEffect(center, position))
                {
                    continue;
                }

                float distance01 = clampedRadius == 0
                    ? 0f
                    : (float)distance / (clampedRadius + 1);
                visitor(index, distance01);
            }
        }
    }

    private bool HasLineOfEffect(Vector2Int from, Vector2Int to)
    {
        int x = from.x;
        int y = from.y;
        int deltaX = Mathf.Abs(to.x - from.x);
        int deltaY = Mathf.Abs(to.y - from.y);
        int stepX = from.x < to.x ? 1 : -1;
        int stepY = from.y < to.y ? 1 : -1;
        int error = deltaX - deltaY;
        while (x != to.x || y != to.y)
        {
            int doubled = error * 2;
            if (doubled > -deltaY)
            {
                error -= deltaY;
                x += stepX;
            }

            if (doubled < deltaX)
            {
                error += deltaX;
                y += stepY;
            }

            Vector2Int position = new Vector2Int(x, y);
            if (!grid.TryGetCellIndex(position, out int index))
            {
                return false;
            }

            if (barriers[index])
            {
                return false;
            }
        }

        return true;
    }

    private void RefreshTopologyIfNeeded()
    {
        if (State.TopologyDirty
            || State.CachedStructuralVersion != grid.StructuralVersion
            || State.CachedBuildingVersion != buildingWorld.BuildingVersion)
        {
            RebuildTopology();
        }
        for (int i = 0; i < operationDoors.Count; i++)
        {
            var entry = operationDoors[i];
            if (entry.Door == null || entry.Revision == entry.Door.OperationRevision) continue;
            foreach (Vector2Int position in entry.Door.buildPoses)
                if (grid.TryGetCellIndex(position, out int index)) State.ClosedDoors[index] = !entry.Door.IsOpen;
            operationDoors[i] = (entry.Door, entry.Door.OperationRevision);
        }
    }

    private void RebuildTopology()
    {
        sources.Clear();
        operationDoors.Clear();
        Array.Clear(barriers, 0, barriers.Length);
        Array.Clear(doors, 0, doors.Length);
        Array.Clear(State.ClosedDoors, 0, State.ClosedDoors.Length);
        Array.Clear(ductExchange, 0, ductExchange.Length);
        for (int index = 0; index < exterior.Length; index++)
        {
            GridCell cell = grid.GetGridCell(
                grid.GetPositionFromCellIndex(index));
            exterior[index] = cell == null
                || cell.AreaType != GridCellAreaType.DungeonInterior;
        }

        IReadOnlyList<BuildableObject> buildings =
            buildingWorld.Buildings ?? Array.Empty<BuildableObject>();
        for (int buildingIndex = 0;
             buildingIndex < buildings.Count;
             buildingIndex++)
        {
            BuildableObject building = buildings[buildingIndex];
            if (building == null
                || building.isDestroy
                || building.BuildingData == null)
            {
                continue;
            }

            IReadOnlyList<Vector2Int> positions =
                building.buildPoses != null && building.buildPoses.Count > 0
                    ? building.buildPoses
                    : new[] { building.centerPos };
            bool wall = RoomDetector.IsWall(building);
            bool door = RoomDetector.IsDoor(building);
            if (building is Door physicalDoor)
                operationDoors.Add((physicalDoor, physicalDoor.OperationRevision));
            BuildingAirDuctAbility duct =
                building.BuildingData.GetAbility<BuildingAirDuctAbility>();
            for (int positionIndex = 0;
                 positionIndex < positions.Count;
                 positionIndex++)
            {
                if (!grid.TryGetCellIndex(positions[positionIndex], out int index))
                {
                    continue;
                }

                barriers[index] |= wall && !door;
                doors[index] |= door;
                State.ClosedDoors[index] |= building is Door leaf && !leaf.IsOpen;
                if (duct != null)
                {
                    ductExchange[index] = Mathf.Max(
                        ductExchange[index],
                        duct.exchangeRate);
                }
            }

            BuildingThermalEmitterAbility thermal =
                building.BuildingData
                    .GetAbility<BuildingThermalEmitterAbility>();
            BuildingAirExchangeAbility airExchange =
                building.BuildingData
                    .GetAbility<BuildingAirExchangeAbility>();
            BuildingLightingAbility lighting =
                building.BuildingData.GetAbility<BuildingLightingAbility>();
            BuildingTemperatureAbility legacyTemperature =
                building.BuildingData.GetAbility<BuildingTemperatureAbility>();

            if (thermal == null
                && airExchange == null
                && lighting == null
                && legacyTemperature == null)
            {
                continue;
            }

            sources.Add(new EnvironmentalFieldSourceDescriptor(
                building,
                building.RequirePersistentInstanceId(),
                building.centerPos,
                thermal,
                airExchange,
                lighting,
                legacyTemperature,
                thermal?.requiresPower == true
                    || airExchange?.requiresPower == true));
        }

        HashSet<BuildingInstanceId> configurableThermostats = sources
            .Where(source => source?.Thermal?.playerConfigurable == true)
            .Select(source => source.BuildingId)
            .ToHashSet();
        foreach (BuildingInstanceId staleOwner in targetOverrides.Keys
                     .Where(id => !configurableThermostats.Contains(id))
                     .ToArray())
        {
            targetOverrides.Remove(staleOwner);
        }

        State.CachedStructuralVersion = grid.StructuralVersion;
        State.CachedBuildingVersion = buildingWorld.BuildingVersion;
        State.TopologyDirty = false;
    }

    private float GetOutdoorTemperature()
    {
        return survivalEnvironment.GetEnvironmentSnapshot().OutdoorTemperature;
    }

    private float GetExteriorAmbientLight()
    {
        return DungeonStory.Environment.EnvironmentalFieldSimulationRules
            .ResolveExteriorAmbientLight(calendar.TimeOfDay == TimeOfDay.Night);
    }

    private static float GetBaseLight(
        bool isExterior,
        float exteriorAmbientLight)
    {
        return DungeonStory.Environment.EnvironmentalFieldSimulationRules
            .GetBaseLight(isExterior, exteriorAmbientLight);
    }

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum
            && value <= maximum;
    }

    private void Touch()
    {
        DungeonStory.Environment.EnvironmentalFieldSimulationRules.Touch(State);
    }

}

internal sealed class EnvironmentalFieldSourceDescriptor
{
    internal EnvironmentalFieldSourceDescriptor(
        BuildableObject building,
        BuildingInstanceId buildingId,
        Vector2Int position,
        BuildingThermalEmitterAbility thermal,
        BuildingAirExchangeAbility air,
        BuildingLightingAbility light,
        BuildingTemperatureAbility legacyTemperature,
        bool requiresPower)
    {
        Building = building;
        BuildingId = buildingId;
        Position = position;
        Thermal = thermal;
        Air = air;
        Light = light;
        LegacyTemperature = legacyTemperature;
        RequiresPower = requiresPower;
        bool consumesFuel = building.BuildingData
            .GetAbility<BuildingFuelConsumerAbility>() != null;
        RequiresFuel = consumesFuel
            && (thermal != null || light != null || legacyTemperature != null);
        ThermalRequiresFuel = thermal != null && consumesFuel;
        LegacyTemperatureRequiresFuel = legacyTemperature != null && consumesFuel;
        LightRequiresPower = light != null
            && building.BuildingData.GetAbility<BuildingPowerConsumerAbility>() != null;
        LightRequiresFuel = light != null && consumesFuel;
        LightVisual = light == null ? null : building.GetComponentInChildren<RoomClippedLight2D>(true);
    }

    internal readonly BuildableObject Building;
    internal readonly BuildingInstanceId BuildingId;
    internal readonly Vector2Int Position;
    internal readonly BuildingThermalEmitterAbility Thermal;
    internal readonly BuildingAirExchangeAbility Air;
    internal readonly BuildingLightingAbility Light;
    internal readonly BuildingTemperatureAbility LegacyTemperature;
    internal readonly bool RequiresPower;
    internal readonly bool RequiresFuel;
    internal readonly bool ThermalRequiresFuel;
    internal readonly bool LegacyTemperatureRequiresFuel;
    internal readonly bool LightRequiresPower;
    internal readonly bool LightRequiresFuel;
    internal readonly RoomClippedLight2D LightVisual;
}
