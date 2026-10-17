using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public readonly struct ProcessAccidentFireReceipt
{
    public ProcessAccidentFireReceipt(
        string workOperationId,
        CharacterActor worker,
        BuildableObject facility,
        WorkTypeId workTypeId,
        string damagedAnatomyNodeId,
        float appliedDamage)
    {
        WorkOperationId = workOperationId?.Trim() ?? string.Empty;
        Worker = worker;
        Facility = facility;
        WorkTypeId = workTypeId;
        DamagedAnatomyNodeId = damagedAnatomyNodeId?.Trim() ?? string.Empty;
        AppliedDamage = appliedDamage;
    }

    public string WorkOperationId { get; }
    public CharacterActor Worker { get; }
    public BuildableObject Facility { get; }
    public WorkTypeId WorkTypeId { get; }
    public string DamagedAnatomyNodeId { get; }
    public float AppliedDamage { get; }
    public bool IsValid =>
        !string.IsNullOrEmpty(WorkOperationId)
        && Worker != null
        && Facility != null
        && Facility.PersistentInstanceId.IsValid
        && WorkTypeId.IsValid
        && !string.IsNullOrEmpty(DamagedAnatomyNodeId)
        && !float.IsNaN(AppliedDamage)
        && !float.IsInfinity(AppliedDamage)
        && AppliedDamage > 0f;
}

public interface IEnvironmentalFireProcessAccidentProducer
{
    EnvironmentalFireIgnitionResult TryPublish(
        in ProcessAccidentFireReceipt receipt);
}

public sealed class ProcessAccidentEnvironmentalFireProducer :
    IEnvironmentalFireProcessAccidentProducer
{
    private readonly IEnvironmentalFireCommand fire;

    public ProcessAccidentEnvironmentalFireProducer(
        IEnvironmentalFireCommand fire)
    {
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
    }

    public EnvironmentalFireIgnitionResult TryPublish(
        in ProcessAccidentFireReceipt receipt)
    {
        if (!receipt.IsValid
            || !CharacterPersistentIdentity.TryGet(
                receipt.Worker,
                out CharacterId workerId))
        {
            return Invalid("A committed work-accident receipt is required.");
        }

        BuildingProcessAccidentFireSourceAbility source =
            receipt.Facility.BuildingData?
                .GetAbility<BuildingProcessAccidentFireSourceAbility>();
        BuildingEnvironmentalFireAbility target = receipt.Facility.BuildingData?
            .GetAbility<BuildingEnvironmentalFireAbility>();
        if (source == null
            || target == null
            || !source.Supports(receipt.WorkTypeId.Value)
            || !target.Accepts(EnvironmentalFireIgnitionKind.ProcessAccident))
        {
            return Invalid(
                "The completed accident has no authored environmental-fire result.");
        }
        source.ValidateOrThrow();
        target.CreateProfileOrThrow();

        string causeId = string.Concat(
            "environmental-fire-process-accident:",
            receipt.WorkOperationId);
        string evidenceId = string.Concat(
            "work-accident-receipt:",
            workerId.Value,
            ":",
            receipt.WorkTypeId.Value,
            ":node=",
            receipt.DamagedAnatomyNodeId,
            ":damage=",
            receipt.AppliedDamage.ToString("R", CultureInfo.InvariantCulture));
        EnvironmentalFireIgnitionResult result = fire.TryIgnite(
            new EnvironmentalFireIgnitionRequest(
                causeId,
                EnvironmentalFireIgnitionKind.ProcessAccident,
                receipt.WorkOperationId,
                new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    receipt.Facility.PersistentInstanceId.Value),
                source.ignitionIntensity,
                evidenceId,
                targetDisplayName: FacilityShopService.GetBuildingName(
                    receipt.Facility.BuildingData)));
        if (result.Disposition ==
            EnvironmentalFireIgnitionDisposition.CauseConflict)
        {
            throw new InvalidOperationException(
                $"Process-accident fire receipt '{causeId}' conflicts with its committed evidence.");
        }
        return result;
    }

    private static EnvironmentalFireIgnitionResult Invalid(string reason) =>
        new(
            EnvironmentalFireIgnitionDisposition.InvalidRequest,
            string.Empty,
            reason);
}

public sealed class ActiveHeatEnvironmentalFireProducer : ITickable
{
    public const string RiskKeyPrefix =
        "environment:fire:active-heat-ignition:";

    private readonly IGameClock clock;
    private readonly IGameCalendar calendar;
    private readonly IPowerInfrastructureQuery power;
    private readonly IBuildingWorldQuery buildings;
    private readonly IRoomLayoutCache rooms;
    private readonly IEnvironmentalFireCommand fire;

    public ActiveHeatEnvironmentalFireProducer(
        IGameClock clock,
        IGameCalendar calendar,
        IPowerInfrastructureQuery power,
        IBuildingWorldQuery buildings,
        IRoomLayoutCache rooms,
        IEnvironmentalFireCommand fire)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
    }

    public void Tick()
    {
        if (clock.IsPaused || !calendar.IsRunning)
        {
            return;
        }
        float delta = clock.DeltaTime;
        if (!FiniteNonNegative(delta)
            || calendar.Day < 1
            || !FiniteNonNegative(calendar.ElapsedSeconds))
        {
            throw new InvalidOperationException(
                "Active-heat fire scheduling requires finite nonnegative game time.");
        }
        if (delta <= 0f)
        {
            return;
        }

        double absoluteNow = Math.Max(
            0d,
            (calendar.Day - 1d) * GameSimulationTimeRules.SecondsPerDay
                + calendar.ElapsedSeconds);
        double absoluteBefore = Math.Max(0d, absoluteNow - delta);
        BuildableObject[] live = buildings.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.PersistentInstanceId.IsValid)
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
        foreach (BuildableObject sourceBuilding in live)
        {
            BuildingActiveHeatFireSourceAbility source =
                sourceBuilding.BuildingData?
                    .GetAbility<BuildingActiveHeatFireSourceAbility>();
            if (source == null)
            {
                continue;
            }
            source.ValidateOrThrow();
            if (source.requiresActiveUser
                && sourceBuilding.CurrentUserCount <= 0)
            {
                continue;
            }

            PowerNodeSnapshot node = null;
            if (source.requiresPower
                && (!power.TryGetNode(sourceBuilding, out node)
                    || !node.Powered
                    || !node.ConnectionEnabled
                    || node.BreakerTripped
                    || !FiniteNonNegative(node.SuppliedFraction)
                    || node.SuppliedFraction <= 0f))
            {
                continue;
            }

            long currentWindow = (long)Math.Floor(
                absoluteNow / source.ignitionWindowSeconds);
            long previousWindow = (long)Math.Floor(
                absoluteBefore / source.ignitionWindowSeconds);
            if (currentWindow <= previousWindow)
            {
                continue;
            }

            BuildableObject target = FindExposedTarget(sourceBuilding, live);
            if (target == null)
            {
                continue;
            }
            for (long window = previousWindow + 1L;
                 window <= currentWindow;
                 window++)
            {
                TryIgniteWindow(sourceBuilding, target, source, node, window);
                if (window == long.MaxValue)
                {
                    break;
                }
            }
        }
    }

    private BuildableObject FindExposedTarget(
        BuildableObject source,
        IReadOnlyList<BuildableObject> live)
    {
        HashSet<Vector2Int> exposedCells = new();
        IReadOnlyList<Vector2Int> footprint = Footprint(source);
        foreach (Vector2Int cell in footprint)
        {
            exposedCells.Add(cell + Vector2Int.left);
            exposedCells.Add(cell + Vector2Int.right);
        }

        return live.Where(candidate =>
                !ReferenceEquals(candidate, source)
                && ReferenceEquals(candidate.Grid, source.Grid)
                && candidate.BuildingData?
                    .GetAbility<BuildingEnvironmentalFireAbility>()
                    is BuildingEnvironmentalFireAbility ability
                && ability.Accepts(
                    EnvironmentalFireIgnitionKind.ActiveHeatSource)
                && Footprint(candidate).Any(exposedCells.Contains)
                && IsSameOpenRoom(source, candidate))
            .OrderBy(
                candidate => candidate.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private bool IsSameOpenRoom(
        BuildableObject source,
        BuildableObject target)
    {
        bool sourceHasRoom = rooms.TryGetRoom(
            source.Grid,
            source.centerPos,
            out RoomInstance sourceRoom);
        bool targetHasRoom = rooms.TryGetRoom(
            target.Grid,
            target.centerPos,
            out RoomInstance targetRoom);
        return sourceHasRoom == targetHasRoom
            && (!sourceHasRoom || ReferenceEquals(sourceRoom, targetRoom));
    }

    private void TryIgniteWindow(
        BuildableObject sourceBuilding,
        BuildableObject targetBuilding,
        BuildingActiveHeatFireSourceAbility source,
        PowerNodeSnapshot node,
        long window)
    {
        string causeId = string.Concat(
            "environmental-fire-active-heat:",
            sourceBuilding.PersistentInstanceId.Value,
            ":",
            source.ignitionWindowSeconds.ToString(
                "R",
                CultureInfo.InvariantCulture),
            ":",
            window);
        float sample = (PersistentEntityId.GetStableHash32(
                RiskKeyPrefix + causeId) & 0x00ffffffu)
            / 16777216f;
        if (sample >= source.ignitionChancePerWindow)
        {
            return;
        }

        string evidenceId = string.Concat(
            "active-heat-window:users=",
            sourceBuilding.CurrentUserCount,
            ":powered=",
            node?.Powered == true ? "1" : "0",
            ":supplied=",
            (node?.SuppliedFraction ?? 0f).ToString(
                "R",
                CultureInfo.InvariantCulture),
            ":target=",
            targetBuilding.PersistentInstanceId.Value,
            ":sample=",
            sample.ToString("R", CultureInfo.InvariantCulture));
        EnvironmentalFireIgnitionResult result = fire.TryIgnite(
            new EnvironmentalFireIgnitionRequest(
                causeId,
                EnvironmentalFireIgnitionKind.ActiveHeatSource,
                sourceBuilding.PersistentInstanceId.Value,
                new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    targetBuilding.PersistentInstanceId.Value),
                source.ignitionIntensity,
                evidenceId,
                targetDisplayName: FacilityShopService.GetBuildingName(
                    targetBuilding.BuildingData)));
        if (result.Disposition ==
            EnvironmentalFireIgnitionDisposition.CauseConflict)
        {
            throw new InvalidOperationException(
                $"Active-heat fire window '{causeId}' conflicts with its deterministic evidence.");
        }
    }

    private static IReadOnlyList<Vector2Int> Footprint(
        BuildableObject building) =>
        building.buildPoses != null && building.buildPoses.Count > 0
            ? building.buildPoses
            : (IReadOnlyList<Vector2Int>)building.BuildingData?
                .GetGridPosList(building.centerPos)
                ?? Array.Empty<Vector2Int>();

    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

public sealed class AuthoredFlameImpactEnvironmentalFireProducer :
    IStartable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly IBuildingWorldQuery buildings;
    private readonly IEnvironmentalFireCommand fire;
    private IDisposable subscription;

    public AuthoredFlameImpactEnvironmentalFireProducer(
        IGameEventBus events,
        IBuildingWorldQuery buildings,
        IEnvironmentalFireCommand fire)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
    }

    public void Start()
    {
        subscription ??= events.Subscribe<DefenseFacilityTriggeredEvent>(
            OnTriggered);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void OnTriggered(DefenseFacilityTriggeredEvent triggered)
    {
        DefenseActivationSnapshot report = triggered.report;
        if (report == null
            || report.EnvironmentalIgnitionIntensity <= 0f
            || report.ActivationCount <= 0
            || string.IsNullOrWhiteSpace(report.SourceFacilityPersistentId)
            || string.IsNullOrWhiteSpace(report.TargetPersistentId)
            || report.SourceFacility == null
            || report.SourceFacility.isDestroy)
        {
            return;
        }

        BuildableObject target = buildings.Buildings
            .Where(candidate => candidate != null
                && !candidate.isDestroy
                && candidate.PersistentInstanceId.IsValid
                && ReferenceEquals(
                    candidate.Grid,
                    report.SourceFacility.Grid)
                && Footprint(candidate).Contains(report.TargetPosition)
                && candidate.BuildingData?
                    .GetAbility<BuildingEnvironmentalFireAbility>()
                    is BuildingEnvironmentalFireAbility ability
                && ability.Accepts(
                    EnvironmentalFireIgnitionKind.AuthoredFlameImpact))
            .OrderBy(
                candidate => candidate.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
        {
            return;
        }

        string causeId = string.Concat(
            "environmental-fire-flame-impact:",
            report.SourceFacilityPersistentId,
            ":",
            report.ActivationCount);
        EnvironmentalFireIgnitionResult result = fire.TryIgnite(
            new EnvironmentalFireIgnitionRequest(
                causeId,
                EnvironmentalFireIgnitionKind.AuthoredFlameImpact,
                report.SourceFacilityPersistentId,
                new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    target.PersistentInstanceId.Value),
                report.EnvironmentalIgnitionIntensity,
                string.Concat(
                    "defense-hit:",
                    report.TargetPersistentId,
                    ":position=",
                    report.TargetPosition.x,
                    ",",
                    report.TargetPosition.y),
                targetDisplayName: FacilityShopService.GetBuildingName(
                    target.BuildingData)));
        if (result.Disposition ==
            EnvironmentalFireIgnitionDisposition.CauseConflict)
        {
            throw new InvalidOperationException(
                $"Authored flame impact '{causeId}' conflicts with its hit receipt.");
        }
    }

    private static IReadOnlyList<Vector2Int> Footprint(
        BuildableObject building) =>
        building.buildPoses != null && building.buildPoses.Count > 0
            ? building.buildPoses
            : (IReadOnlyList<Vector2Int>)building.BuildingData?
                .GetGridPosList(building.centerPos)
                ?? Array.Empty<Vector2Int>();
}

public sealed class SeasonalFeedSelfHeatingEnvironmentalFireProducer :
    IStartable,
    IDisposable
{
    private readonly IWorldEventCatalog seasonalCatalog;
    private readonly ISeasonalFeedSelfHeatingTargetQuery targets;
    private readonly IEnvironmentalFireCommand fire;
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public SeasonalFeedSelfHeatingEnvironmentalFireProducer(
        IWorldEventCatalog seasonalCatalog,
        ISeasonalFeedSelfHeatingTargetQuery targets,
        IEnvironmentalFireCommand fire,
        IGameEventBus events)
    {
        this.seasonalCatalog = seasonalCatalog
            ?? throw new ArgumentNullException(nameof(seasonalCatalog));
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        subscription ??= events.Subscribe<V20ContentEffectsResolvedEvent>(
            OnContentResolved);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void OnContentResolved(V20ContentEffectsResolvedEvent resolved)
    {
        SeasonalWorldEventDefinitionSO definition = seasonalCatalog
            .SeasonalEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.StableId,
                    resolved.DefinitionId,
                    StringComparison.Ordinal));
        SeasonalFeedSelfHeatingFireProfile profile =
            definition?.feedSelfHeatingFireProfile;
        if (profile?.IsConfigured != true
            || !resolved.PhysicalEffectsApplied
            || string.IsNullOrWhiteSpace(resolved.OccurrenceInstanceId)
            || !(resolved.Effects ?? Array.Empty<V20ContentEffect>()).Any(
                value => value != null
                    && value.kind == V20ContentEffectKind.Threat
                    && value.amount > 0f))
        {
            return;
        }
        IReadOnlyList<string> errors = profile.Validate(definition.StableId);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }

        if (!targets.TrySelect(
                profile,
                out SeasonalFeedSelfHeatingTarget selected,
                out SeasonalFeedSelfHeatingTargetFailure selectionFailure))
        {
            throw new InvalidOperationException(
                $"Feed self-heating occurrence '{resolved.OccurrenceInstanceId}' "
                + $"lost its required physical target: {selectionFailure.Code}: "
                + selectionFailure.Reason);
        }
        if (!targets.TryRevalidate(
                selected,
                profile,
                out SeasonalFeedSelfHeatingTarget candidate,
                out SeasonalFeedSelfHeatingTargetFailure validationFailure))
        {
            throw new InvalidOperationException(
                $"Feed self-heating occurrence '{resolved.OccurrenceInstanceId}' "
                + $"will not retarget after selection: {validationFailure.Code}: "
                + validationFailure.Reason);
        }

        string causeId = string.Concat(
            "environmental-fire-feed-self-heating:",
            resolved.OccurrenceInstanceId);
        EnvironmentalFireIgnitionResult result = fire.TryIgnite(
            new EnvironmentalFireIgnitionRequest(
                causeId,
                EnvironmentalFireIgnitionKind.FeedSelfHeating,
                resolved.OccurrenceInstanceId,
                new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    candidate.FacilityInstanceId.Value),
                profile.ignitionIntensity,
                string.Concat(
                    "physical-feed-stack:",
                    candidate.StackId,
                    ":item=",
                    candidate.ItemId,
                    ":quantity=",
                    candidate.Quantity,
                    ":destination=",
                    candidate.DestinationId),
                new EnvironmentalFireFuelLossRequest(
                    new EnvironmentalFireTargetRef(
                        EnvironmentalFireTargetKind.ItemStack,
                        candidate.StackId),
                    candidate.Quantity),
                candidate.FacilityDisplayName));
        if (result.Disposition ==
            EnvironmentalFireIgnitionDisposition.CauseConflict)
        {
            throw new InvalidOperationException(
                $"Feed self-heating occurrence '{causeId}' conflicts with its physical-stock receipt.");
        }
        if (result.Disposition
            is EnvironmentalFireIgnitionDisposition.TargetMissing
            or EnvironmentalFireIgnitionDisposition.TargetNotCombustible)
        {
            throw new InvalidOperationException(
                $"Feed self-heating occurrence '{causeId}' lost selected target "
                + $"'{candidate.FacilityInstanceId.Value}' before ignition: "
                + $"{result.Disposition}: {result.Reason}");
        }
        if (result.Disposition
            == EnvironmentalFireIgnitionDisposition.FuelUnavailable)
        {
            throw new InvalidOperationException(
                $"Feed self-heating occurrence '{causeId}' could not commit exact "
                + $"physical stock '{candidate.StackId}' x{candidate.Quantity}: "
                + result.Reason);
        }
    }
}
