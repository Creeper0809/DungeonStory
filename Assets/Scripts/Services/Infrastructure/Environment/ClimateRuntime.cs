using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class ResourceClimateDefinitionCatalog : IClimateDefinitionCatalog
{
    private readonly Dictionary<string, ClimateZoneDefinition> zones;
    private readonly Dictionary<string, WeatherFrontDefinition> fronts;

    public ResourceClimateDefinitionCatalog(IGameContentCatalog content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        zones = content.GetAll<ClimateZoneDefinitionSO>()
            .Select(value => value.CreateRuntimeDefinition())
            .ToDictionary(value => value.Id, StringComparer.Ordinal);
        fronts = content.GetAll<WeatherFrontDefinitionSO>()
            .Select(value => value.CreateRuntimeDefinition())
            .ToDictionary(value => value.Id, StringComparer.Ordinal);
        if (zones.Count != 5 || fronts.Count != 6
            || zones.Values.Any(value => !value.IsValid)
            || fronts.Values.Any(value => !value.IsValid))
        {
            throw new InvalidOperationException(
                $"V19 climate content requires exactly 5 zones and 6 fronts; "
                + $"found {zones.Count} zones and {fronts.Count} fronts.");
        }
        for (int season = 0; season < 4; season++)
        {
            float total = fronts.Values.Sum(value => value.GetWeight((Season)season));
            if (Math.Abs(total - 100f) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Weather weights for {(Season)season} must total 100, found {total}.");
            }
        }
        Fronts = fronts.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<WeatherFrontDefinition> Fronts { get; }

    public ClimateZoneDefinition RequireZone(string id) =>
        zones.TryGetValue(id?.Trim() ?? string.Empty, out ClimateZoneDefinition value)
            ? value
            : throw new KeyNotFoundException($"Unknown climate zone '{id}'.");

    public WeatherFrontDefinition RequireFront(string id) =>
        fronts.TryGetValue(id?.Trim() ?? string.Empty, out WeatherFrontDefinition value)
            ? value
            : throw new KeyNotFoundException($"Unknown weather front '{id}'.");
}

public sealed class ClimateRuntime :
    IClimateQuery,
    IWeatherForecastQuery,
    IClimatePersistence,
    IStartable,
    IDisposable
{
    public const string DefaultClimateZoneId = "climate:temperate-cave";
    private const string ClimateRandomStreamId = "world:climate";
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IClimateDefinitionCatalog definitions;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private readonly IRandomStream random;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly ClimateDurableEquipmentRuntime observationEquipment;
    private IDisposable dayEndedSubscription;
    private int version = 1;

    public ClimateRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        IClimateDefinitionCatalog definitions,
        IGameCalendar calendar,
        IGameEventBus events,
        IRandomStreamProvider randomStreams,
        IFacilityCapabilityQuery facilities,
        IDurableFacilityEquipmentPolicyQuery equipmentPolicies,
        IDurableFacilityEquipmentSlotCommand equipmentSlots,
        IDurableFacilityEquipmentSlotQuery equipmentSlotQuery,
        IDurableFacilityEquipmentUseCommand equipmentUse)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(ClimateRandomStreamId);
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        observationEquipment = new ClimateDurableEquipmentRuntime(
            equipmentPolicies,
            equipmentSlots,
            equipmentSlotQuery,
            equipmentUse);
    }

    public int Version => version;
    public int AbsoluteDay => Current.AbsoluteDay;
    public string ClimateZoneId => Current.ClimateZoneId;
    public string WeatherFrontId => Current.WeatherFrontId;
    public int FrontRemainingDays => Current.FrontRemainingDays;
    public float OutdoorTemperatureC => Current.GetOutdoorTemperature(definitions);
    public bool ObservationToolsOperational => TryResolveObservationTower(
            out BuildableObject tower)
        && observationEquipment.IsOperational(
            tower.RequirePersistentInstanceId());
    public int ForecastHorizonDays => ObservationToolsOperational
        ? Math.Min(3, Math.Max(1, Current.FrontRemainingDays))
        : 0;

    public void Start()
    {
        _ = Current;
        MaintainForecastEquipment(wear: false);
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    public ClimateWorldSaveData Capture() => Current.Capture();

    public ClimateAggregateState PrepareRestore(ClimateWorldSaveData data) =>
        ClimateAggregateState.Restore(data, definitions);

    public void PublishRestore(ClimateAggregateState candidate)
    {
        rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
        version = unchecked(version + 1);
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        int nextDay = ended.day + 1;
        if (nextDay < Current.AbsoluteDay)
        {
            throw new InvalidOperationException(
                "Operating-day events cannot move climate backward.");
        }
        Writable.AdvanceToDay(nextDay, definitions, () => random.NextFloat());
        MaintainForecastEquipment(wear: true);
        version = unchecked(version + 1);
    }

    private void MaintainForecastEquipment(bool wear)
    {
        BuildableObject tower = facilities.FindOperational(
                FacilityCapabilityKind.None,
                "building:8851")
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (tower == null)
        {
            return;
        }

        observationEquipment.TryMaintain(
            tower.RequirePersistentInstanceId(),
            tower.centerPos,
            wear);
    }

    private bool TryResolveObservationTower(out BuildableObject tower)
    {
        tower = facilities.FindOperational(
                FacilityCapabilityKind.None,
                "building:8851")
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        return tower != null;
    }

    private ClimateAggregateState Current => rootStore.GetOrCreate(() =>
        ClimateAggregateState.Create(
            calendar.Day,
            DefaultClimateZoneId,
            definitions,
            () => random.NextFloat()));

    private ClimateAggregateState Writable => rootStore.GetOrCreateWritable(
        () => ClimateAggregateState.Create(
            calendar.Day,
            DefaultClimateZoneId,
            definitions,
            () => random.NextFloat()),
        value => ClimateAggregateState.Restore(value.Capture(), definitions));
}
