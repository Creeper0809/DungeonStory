#if UNITY_EDITOR
using System;
using DungeonStory.Foundation;

public static class WorldItemEditorTestFactory
{
    public static WorldItemStackRuntime Create(
        IGridSystemProvider gridProvider,
        IDungeonItemCatalogProvider catalog,
        IItemHaulingSettingsProvider haulingSettings,
        ICharacterIdRegistry characterIds,
        IWorldDropZoneQuery dropZones,
        ICharacterSpawnerProvider characterSpawner,
        IGridPathSearchBroker pathSearch,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock clock,
        WorldItemRepository repository,
        IItemReservationService reservations,
        IWorldItemSpawner spawner,
        WorldItemQueryService queries,
        IWorldItemHaulPlanningService haulPlanning,
        IItemMarkerPresenter itemMarkerPresenter,
        IItemTransferService itemTransferService,
        ICharacterAiPerformanceRecorder performanceRecorder,
        IItemQuantityReservationPersistence reservationPersistence = null,
        IFacilityOutputExactRouteOutboxPersistence
            exactRouteOutboxPersistence = null,
        IPhysicalItemBatchDispositionService batchDispositionOverride = null)
    {
        _ = pathSearch ?? throw new ArgumentNullException(nameof(pathSearch));
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(catalog);
        IFacilityOutputExactRouteOutboxPersistence exactRouteAuthority =
            exactRouteOutboxPersistence
            ?? new FacilityOutputExactRouteService(
                repository,
                massQuery,
                itemMarkerPresenter);
        WorldItemPersistenceService persistence = new WorldItemPersistenceService(
            new EquipmentEvolutionFormulaCatalogs(
                CharacterAiEditorTestDependencies.ContentDefinitions),
            catalog,
            haulingSettings,
            repository,
            exactRouteAuthority,
            reservationPersistence,
            reservationPersistence as IItemReservationMutationGate);
        WorldItemWarehouseService warehouses = new WorldItemWarehouseService(
            catalog,
            repository,
            worldRegistry,
            spawner,
            itemMarkerPresenter,
            gridProvider,
            characterIds,
            reservations);
        WorldItemTheftService theft = new WorldItemTheftService(
            gridProvider,
            catalog,
            haulingSettings,
            clock,
            repository,
            queries,
            itemMarkerPresenter);
        WorldItemReadServices reads = new WorldItemReadServices(
            catalog,
            massQuery,
            haulingSettings,
            queries,
            itemMarkerPresenter,
            performanceRecorder,
            DisabledDungeonDebugRuleQuery.Instance,
            new FacilityOutputClearanceTelemetryRuntime());
        WorldItemMutationServices mutations = new WorldItemMutationServices(
            repository,
            reservations,
            spawner,
            haulPlanning,
            itemTransferService,
            theft);
        IPhysicalItemBatchDispositionService batchDispositions =
            batchDispositionOverride ?? new PhysicalItemBatchDispositionService(
                repository,
                massQuery,
                itemMarkerPresenter, clock: new DungeonStory.Foundation.UnityUiClock());
        return new WorldItemStackRuntime(
            gridProvider,
            characterIds,
            dropZones,
            characterSpawner,
            reads,
            mutations,
            persistence,
            warehouses,
            batchDispositions);
    }
}
#endif
