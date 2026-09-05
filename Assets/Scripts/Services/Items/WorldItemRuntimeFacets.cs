using System;

public sealed class WorldItemReadServices
{
    public WorldItemReadServices(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery mass,
        IItemHaulingSettingsProvider haulingSettings,
        WorldItemQueryService queries,
        IItemMarkerPresenter markers,
        ICharacterAiPerformanceRecorder performance,
        IDungeonDebugRuleQuery debugRules,
        IFacilityOutputClearanceTelemetrySink outputClearanceTelemetry)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Mass = mass ?? throw new ArgumentNullException(nameof(mass));
        HaulingSettings = haulingSettings
            ?? throw new ArgumentNullException(nameof(haulingSettings));
        Queries = queries ?? throw new ArgumentNullException(nameof(queries));
        Markers = markers ?? throw new ArgumentNullException(nameof(markers));
        Performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        DebugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        OutputClearanceTelemetry = outputClearanceTelemetry
            ?? throw new ArgumentNullException(nameof(outputClearanceTelemetry));
    }

    public IDungeonItemCatalogProvider Catalog { get; }
    public IPhysicalItemMassQuery Mass { get; }
    public IItemHaulingSettingsProvider HaulingSettings { get; }
    public WorldItemQueryService Queries { get; }
    public IItemMarkerPresenter Markers { get; }
    public ICharacterAiPerformanceRecorder Performance { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
    public IFacilityOutputClearanceTelemetrySink OutputClearanceTelemetry { get; }
}

public sealed class WorldItemMutationServices
{
    public WorldItemMutationServices(
        WorldItemRepository repository,
        IItemReservationService reservations,
        IWorldItemSpawner spawner,
        IWorldItemHaulPlanningService haulPlanning,
        IItemTransferService transfers,
        WorldItemTheftService theft)
    {
        Repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        Reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        Spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        HaulPlanning = haulPlanning
            ?? throw new ArgumentNullException(nameof(haulPlanning));
        Transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        Theft = theft ?? throw new ArgumentNullException(nameof(theft));
    }

    public WorldItemRepository Repository { get; }
    public IItemReservationService Reservations { get; }
    public IWorldItemSpawner Spawner { get; }
    public IWorldItemHaulPlanningService HaulPlanning { get; }
    public IItemTransferService Transfers { get; }
    public WorldItemTheftService Theft { get; }
}
