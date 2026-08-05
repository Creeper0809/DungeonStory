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
        ICharacterAiPerformanceRecorder performanceRecorder)
    {
        _ = pathSearch ?? throw new ArgumentNullException(nameof(pathSearch));
        WorldItemPersistenceService persistence = new WorldItemPersistenceService(
            catalog,
            haulingSettings,
            repository);
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
            haulingSettings,
            queries,
            itemMarkerPresenter,
            performanceRecorder,
            DisabledDungeonDebugRuleQuery.Instance);
        WorldItemMutationServices mutations = new WorldItemMutationServices(
            repository,
            reservations,
            spawner,
            haulPlanning,
            itemTransferService,
            theft);
        return new WorldItemStackRuntime(
            gridProvider,
            characterIds,
            dropZones,
            characterSpawner,
            reads,
            mutations,
            persistence,
            warehouses);
    }
}
#endif
