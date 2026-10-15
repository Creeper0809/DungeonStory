using System;
using System.Collections.Generic;
using System.Linq;

// Only valid inside a synchronous owner transaction. The owner blocks snapshots
// until it either rolls back or accepts; no transient handle is saved.
public interface IRetailStockSinkTransaction
{
    void Accept();
    bool TryRollback(out string failure);
}
public interface IReversibleRetailEquipmentAuthority
{
    bool TryConsumeRetailStockReversible(string instanceId, string sourceOperationId,
        out IRetailStockSinkTransaction transaction, out string failure);
}
public interface IReversibleRetailStockPhysicalRuntime
{
    bool TryCommitExternalSinkReversible(RetailStockLotSnapshot lot,
        out IRetailStockSinkTransaction transaction, out string failure);
}

// Shared local mutation implementation for the retail and combat owners of the
// same repository. It has no event bus, native outcome or save authority.
internal sealed class RetailEquipmentSinkTransaction : IRetailStockSinkTransaction
{
    private readonly IItemInstanceRepository repository;
    private readonly CombatEquipmentInstance instance;
    private readonly EquipmentModuleInstance[] modules;
    private bool accepted, rolledBack;
    private RetailEquipmentSinkTransaction(IItemInstanceRepository repository,
        CombatEquipmentInstance instance, EquipmentModuleInstance[] modules)
    { this.repository = repository; this.instance = instance; this.modules = modules; }

    internal static bool TryApply(IItemInstanceRepository repository,
        IEnumerable<CharacterCombatLoadoutState> loadouts, string id, string operation,
        out IRetailStockSinkTransaction transaction, out string failure)
    {
        transaction = null; failure = string.Empty;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(operation)
            || !repository.EquipmentInstances.TryGetValue(id, out var live)
            || live.worldState != CombatEquipmentWorldState.RetailStock
            || live.sourceStackId != operation || !string.IsNullOrWhiteSpace(live.ownerCharacterId))
        { failure = "retail-stock-equipment-authority-mismatch"; return false; }
        // A valid retail instance is not equipped. Do not silently edit unrelated loadouts.
        if (loadouts.Where(value => value != null).SelectMany(value => value.profiles ?? new())
            .Any(profile => profile != null && ((profile.weaponInstanceIds?.Contains(id) ?? false)
                || (profile.armorInstanceIds?.Contains(id) ?? false) || profile.shieldInstanceId == id
                || profile.activeWeaponInstanceId == id)))
        { failure = "retail-stock-equipment-still-in-loadout"; return false; }
        var ids = (live.moduleSlots ?? new()).Where(value => !string.IsNullOrWhiteSpace(value?.moduleInstanceId))
            .Select(value => value.moduleInstanceId).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Any(module => !repository.EquipmentModules.TryGetValue(module, out var value) || value == null))
        { failure = "retail-stock-attached-module-missing"; return false; }
        var saved = live.Clone();
        var modules = ids.Select(module => repository.EquipmentModules[module].Clone()).ToArray();
        var result = new RetailEquipmentSinkTransaction(repository, saved, modules);
        // All allocations and validation precede the local, non-yielding dictionary removal.
        repository.EquipmentInstances.Remove(id);
        foreach (string module in ids) repository.EquipmentModules.Remove(module);
        transaction = result;
        return true;
    }
    public void Accept()
    {
        if (rolledBack) throw new InvalidOperationException("retail-sink-already-rolled-back");
        accepted = true;
    }
    public bool TryRollback(out string failure)
    {
        failure = string.Empty;
        if (accepted) { failure = "retail-sink-already-accepted"; return false; }
        if (rolledBack) return true;
        if (repository.EquipmentInstances.ContainsKey(instance.instanceId)
            || modules.Any(module => repository.EquipmentModules.ContainsKey(module.instanceId)))
        { failure = "retail-sink-rollback-identity-conflict"; return false; }
        repository.EquipmentInstances.Add(instance.instanceId, instance);
        foreach (var module in modules) repository.EquipmentModules.Add(module.instanceId, module);
        rolledBack = true; return true;
    }

    internal static IRetailStockSinkTransaction NoSecondaryOwner() => new Empty();
    private sealed class Empty : IRetailStockSinkTransaction
    {
        private bool accepted, rolledBack;
        public void Accept()
        { if (rolledBack) throw new InvalidOperationException("retail-sink-already-rolled-back"); accepted = true; }
        public bool TryRollback(out string failure)
        { failure = accepted ? "retail-sink-already-accepted" : string.Empty; if (!accepted) rolledBack = true; return !accepted; }
    }
}
