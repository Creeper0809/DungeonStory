#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RetailReversibleSinkDebugScenarios
{
    public static void RunForBatch()
    {
        const string DefinitionId = "weapon:greatsword";
        const string OperationId = "retail-stock:test:reversible-sink:1";
        const string ModuleId = "item-instance:retail-sink-module";

        var root = new DungeonRuntimeAggregateRootStore();
        var repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            root);
        var catalog = new ResourceCombatEquipmentCatalog(
            new ResourceGameContentCatalog(
                new UnityGameContentRootLoader()));
        var authority = new RetailEquipmentAuthority(
            catalog,
            repository,
            new CombatEquipmentLoadoutStore(
                new CombatEquipmentRuntimeStateStore(root)));
        var physical = new RetailStockPhysicalRuntime(authority);

        Require(
            physical.TryCreateAuthoredUniqueLot(
                1,
                PhysicalItemIds.ForEquipment(DefinitionId),
                4600L,
                OperationId,
                out RetailStockLotSnapshot lot,
                out string failure),
            "Authored retail equipment lot creation failed: " + failure);
        Require(
            repository.EquipmentInstances.TryGetValue(
                lot.itemInstanceId,
                out CombatEquipmentInstance equipment)
            && equipment.worldState == CombatEquipmentWorldState.RetailStock
            && string.Equals(
                equipment.sourceStackId,
                OperationId,
                StringComparison.Ordinal)
            && equipment.moduleSlots.Count > 0,
            "The authored retail equipment did not enter the exact RetailStock authority.");

        var module = new EquipmentModuleInstance
        {
            instanceId = ModuleId,
            definitionId = "module:weapon:balanced-core",
            grade = 2,
            condition = 0.91f,
            identified = true,
            state = EquipmentModuleProcessState.Installed,
            attachedEquipmentInstanceId = equipment.instanceId
        };
        repository.EquipmentModules.Add(ModuleId, module);
        equipment.moduleSlots[0].moduleInstanceId = ModuleId;
        RefreshFrozenLot(lot, equipment, module);

        string equipmentBefore = JsonUtility.ToJson(equipment.Clone());
        string moduleBefore = JsonUtility.ToJson(module.Clone());
        string fingerprintBefore = lot.componentFingerprint;

        RetailStockLotSnapshot tampered = lot.Clone();
        tampered.componentFingerprint += ":tampered";
        Require(
            !physical.TryCommitExternalSinkReversible(
                tampered,
                out IRetailStockSinkTransaction invalidSink,
                out failure)
            && invalidSink == null
            && string.Equals(
                failure,
                "retail-terminal-component-fingerprint-mismatch",
                StringComparison.Ordinal)
            && HasExactState(
                repository,
                equipment.instanceId,
                ModuleId,
                equipmentBefore,
                moduleBefore),
            "A mismatched retail fingerprint changed the equipment authority.");

        Require(
            physical.TryCommitExternalSinkReversible(
                lot,
                out IRetailStockSinkTransaction rollbackSink,
                out failure),
            "Reversible retail sink failed: " + failure);
        Require(
            rollbackSink != null
            && !repository.EquipmentInstances.ContainsKey(equipment.instanceId)
            && !repository.EquipmentModules.ContainsKey(ModuleId),
            "The reversible retail sink retained equipment or its attached module.");
        Require(
            rollbackSink.TryRollback(out failure)
            && string.IsNullOrEmpty(failure)
            && HasExactState(
                repository,
                equipment.instanceId,
                ModuleId,
                equipmentBefore,
                moduleBefore)
            && string.Equals(
                CaptureFingerprint(repository, equipment.instanceId, ModuleId),
                fingerprintBefore,
                StringComparison.Ordinal),
            "Retail sink rollback did not restore the same equipment/module content: "
                + failure);
        Require(
            rollbackSink.TryRollback(out failure)
            && string.IsNullOrEmpty(failure)
            && HasExactState(
                repository,
                equipment.instanceId,
                ModuleId,
                equipmentBefore,
                moduleBefore),
            "Duplicate retail sink rollback was not idempotent: " + failure);

        Require(
            physical.TryCommitExternalSinkReversible(
                lot,
                out IRetailStockSinkTransaction acceptedSink,
                out failure),
            "Accepted retail sink preparation failed: " + failure);
        acceptedSink.Accept();
        Require(
            !acceptedSink.TryRollback(out failure)
            && string.Equals(
                failure,
                "retail-sink-already-accepted",
                StringComparison.Ordinal)
            && !repository.EquipmentInstances.ContainsKey(equipment.instanceId)
            && !repository.EquipmentModules.ContainsKey(ModuleId),
            "An accepted retail sink restored consumed authority.");
    }

    private static void RefreshFrozenLot(
        RetailStockLotSnapshot lot,
        CombatEquipmentInstance equipment,
        EquipmentModuleInstance module)
    {
        ItemInstanceComponentSaveData component =
            EquipmentItemStateCodec.Encode(equipment, new[] { module });
        lot.componentFingerprint = ItemStackSignature.Create(
            lot.itemDefinitionId,
            new[] { component });
        lot.components = new List<RetailStockComponentSnapshot>
        {
            new RetailStockComponentSnapshot
            {
                componentTypeId = component.componentTypeId,
                schemaVersion = component.schemaVersion,
                affectsStacking = component.affectsStacking,
                values = (component.values ?? new List<ItemStateValueSaveData>())
                    .Where(value => value != null)
                    .OrderBy(value => value.key, StringComparer.Ordinal)
                    .Select(value => new RetailStockComponentValueSnapshot
                    {
                        key = value.key,
                        kind = (int)value.kind,
                        stringValue = value.stringValue,
                        integerValue = value.integerValue,
                        decimalValue = value.decimalValue,
                        booleanValue = value.booleanValue
                    })
                    .ToList()
            }
        };
    }

    private static bool HasExactState(
        WorldItemRepository repository,
        string equipmentId,
        string moduleId,
        string equipmentJson,
        string moduleJson) =>
        repository.EquipmentInstances.TryGetValue(
            equipmentId,
            out CombatEquipmentInstance equipment)
        && repository.EquipmentModules.TryGetValue(
            moduleId,
            out EquipmentModuleInstance module)
        && string.Equals(
            JsonUtility.ToJson(equipment),
            equipmentJson,
            StringComparison.Ordinal)
        && string.Equals(
            JsonUtility.ToJson(module),
            moduleJson,
            StringComparison.Ordinal);

    private static string CaptureFingerprint(
        WorldItemRepository repository,
        string equipmentId,
        string moduleId)
    {
        CombatEquipmentInstance equipment =
            repository.EquipmentInstances[equipmentId];
        EquipmentModuleInstance module = repository.EquipmentModules[moduleId];
        return ItemStackSignature.Create(
            PhysicalItemIds.ForEquipment(equipment.definitionId),
            new[] { EquipmentItemStateCodec.Encode(equipment, new[] { module }) });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
