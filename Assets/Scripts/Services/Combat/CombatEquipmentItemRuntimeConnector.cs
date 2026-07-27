using System;
using VContainer.Unity;

public sealed class CombatEquipmentItemRuntimeConnector : IStartable
{
    private readonly CombatEquipmentRuntime equipment;
    private readonly IWorldItemStackRuntime items;

    public CombatEquipmentItemRuntimeConnector(
        CombatEquipmentRuntime equipment,
        IWorldItemStackRuntime items)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.items = items
            ?? throw new ArgumentNullException(nameof(items));
    }

    public void Start()
    {
        equipment.BindItemStackRuntime(items);
    }
}
