using System;
using DungeonStory.Foundation;

public sealed class EquipmentMaintenanceItemServices
{
    public EquipmentMaintenanceItemServices(
        ICombatEquipmentRuntime equipment,
        ICombatEquipmentCatalog equipmentCatalog,
        IResourceEconomyContentCatalog resourceCatalog,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService batchDispositions,
        ICombatEquipmentPickupRuntime equipmentPickup,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        IFacilityBufferDestinationLifecycleCommand destinationLifecycle,
        IFacilityBufferMassCapacityAuthorityQuery destinationCapacities)
    {
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        EquipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        ResourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        BatchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        EquipmentPickup = equipmentPickup
            ?? throw new ArgumentNullException(nameof(equipmentPickup));
        DestinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        DestinationLifecycle = destinationLifecycle
            ?? throw new ArgumentNullException(nameof(destinationLifecycle));
        DestinationCapacities = destinationCapacities
            ?? throw new ArgumentNullException(nameof(destinationCapacities));
    }

    public ICombatEquipmentRuntime Equipment { get; }
    public ICombatEquipmentCatalog EquipmentCatalog { get; }
    public IResourceEconomyContentCatalog ResourceCatalog { get; }
    public IWorldItemStackRuntime Items { get; }
    public IPhysicalItemBatchDispositionService BatchDispositions { get; }
    public ICombatEquipmentPickupRuntime EquipmentPickup { get; }
    public IFacilityBufferDestinationClaimQuery DestinationClaims { get; }
    public IFacilityBufferDestinationLifecycleCommand DestinationLifecycle { get; }
    public IFacilityBufferMassCapacityAuthorityQuery DestinationCapacities { get; }
}

public sealed class EquipmentMaintenanceWorldServices
{
    public EquipmentMaintenanceWorldServices(
        ICharacterAiWorldRegistry worldRegistry,
        IDefenseEngagementStore defenseEngagements)
    {
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        DefenseEngagements = defenseEngagements
            ?? throw new ArgumentNullException(nameof(defenseEngagements));
    }

    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public IDefenseEngagementStore DefenseEngagements { get; }
}

public sealed class EquipmentMaintenanceClockServices
{
    public EquipmentMaintenanceClockServices(IGameClock gameClock, IUiClock uiClock)
    {
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        UiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public IGameClock GameClock { get; }
    public IUiClock UiClock { get; }
}
