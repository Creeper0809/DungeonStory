using System;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class CombatEquipmentCharacterDeathConnector :
    IStartable,
    IDisposable
{
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IGameEventBus events;
    private IDisposable deathSubscription;

    public CombatEquipmentCharacterDeathConnector(
        ICombatEquipmentRuntime equipment,
        IGameEventBus events)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.events = events
            ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        deathSubscription = events.Subscribe<CharacterDeathEvent>(
            OnCharacterDeath);
    }

    public void Dispose()
    {
        deathSubscription?.Dispose();
        deathSubscription = null;
    }

    private void OnCharacterDeath(CharacterDeathEvent gameEvent)
    {
        equipment.HandleCharacterDeath(
            gameEvent.Actor?.Identity?.PersistentId);
    }
}
