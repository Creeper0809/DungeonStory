using System;
using DungeonStory.Foundation;

public sealed class EquipmentMaintenanceItemServices
{
    public EquipmentMaintenanceItemServices(
        ICombatEquipmentRuntime equipment,
        ICombatEquipmentCatalog equipmentCatalog,
        IResourceEconomyContentCatalog resourceCatalog,
        IWorldItemStackRuntime items,
        ICombatEquipmentPickupRuntime equipmentPickup,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        IFacilityBufferDestinationClaimCommand destinationClaimCommands)
    {
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        EquipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        ResourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        EquipmentPickup = equipmentPickup
            ?? throw new ArgumentNullException(nameof(equipmentPickup));
        DestinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        DestinationClaimCommands = destinationClaimCommands
            ?? throw new ArgumentNullException(nameof(destinationClaimCommands));
    }

    public ICombatEquipmentRuntime Equipment { get; }
    public ICombatEquipmentCatalog EquipmentCatalog { get; }
    public IResourceEconomyContentCatalog ResourceCatalog { get; }
    public IWorldItemStackRuntime Items { get; }
    public ICombatEquipmentPickupRuntime EquipmentPickup { get; }
    public IFacilityBufferDestinationClaimQuery DestinationClaims { get; }
    public IFacilityBufferDestinationClaimCommand DestinationClaimCommands { get; }
}

public sealed class EquipmentMaintenanceWorldServices
{
    public EquipmentMaintenanceWorldServices(
        ICharacterAiWorldRegistry worldRegistry,
        IDefenseEngagementRuntime defenseRuntime)
    {
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        DefenseRuntime = defenseRuntime
            ?? throw new ArgumentNullException(nameof(defenseRuntime));
    }

    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public IDefenseEngagementRuntime DefenseRuntime { get; }
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
