using System;

/// <summary>
/// Explicitly composed equipment sub-runtimes. The composition root owns their
/// construction so CombatEquipmentRuntime cannot silently substitute policies.
/// </summary>
public sealed class CombatEquipmentRuntimeCollaborators
{
    public CombatEquipmentRuntimeCollaborators(
        CombatEquipmentStatProjector statProjector,
        CombatEquipmentPhysicalStateWriter physicalState,
        CombatEquipmentLoadoutStore loadoutStore,
        EquipmentModuleRuntime moduleRuntime,
        EquipmentHistoryTransferRuntime historyRuntime,
        CombatEquipmentRuntimeStateStore stateStore)
    {
        StatProjector = statProjector
            ?? throw new ArgumentNullException(nameof(statProjector));
        PhysicalState = physicalState
            ?? throw new ArgumentNullException(nameof(physicalState));
        LoadoutStore = loadoutStore
            ?? throw new ArgumentNullException(nameof(loadoutStore));
        ModuleRuntime = moduleRuntime
            ?? throw new ArgumentNullException(nameof(moduleRuntime));
        HistoryRuntime = historyRuntime
            ?? throw new ArgumentNullException(nameof(historyRuntime));
        StateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public CombatEquipmentStatProjector StatProjector { get; }
    public CombatEquipmentPhysicalStateWriter PhysicalState { get; }
    public CombatEquipmentLoadoutStore LoadoutStore { get; }
    public EquipmentModuleRuntime ModuleRuntime { get; }
    public EquipmentHistoryTransferRuntime HistoryRuntime { get; }
    public CombatEquipmentRuntimeStateStore StateStore { get; }
}
