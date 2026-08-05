#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EquipmentItemStateV18DebugScenarios
{
    [MenuItem("DungeonStory/Debug/Combat/Run V18 Equipment Item State Contracts")]
    public static void RunAll()
    {
        CombatEquipmentInstance source = new()
        {
            instanceId = "item-instance:test-equipment-state",
            definitionId = "weapon:test-v18",
            materialId = "material:test-steel",
            quality = CombatEquipmentQuality.Masterwork,
            durabilityRatio = 0.42f,
            loadedAmmo = 7,
            worldState = CombatEquipmentWorldState.Stored,
            ownerCharacterId = "character:test-owner",
            sourceStackId = "stack:test-equipment-state",
            evolution = new EquipmentEvolutionState(),
            moduleSlots = new List<EquipmentModuleSlotState>
            {
                new EquipmentModuleSlotState
                {
                    slotIndex = 0,
                    moduleInstanceId = "module:test-v18"
                }
            }
        };

        EquipmentModuleInstance attachedModule = new()
        {
            instanceId = "module:test-v18",
            definitionId = "module-definition:test-v18",
            grade = 3,
            condition = 0.83f,
            identified = true,
            attachedEquipmentInstanceId = source.instanceId,
            state = EquipmentModuleProcessState.Installed
        };
        ItemInstanceComponentSaveData encoded =
            EquipmentItemStateCodec.Encode(source, new[] { attachedModule });
        Require(encoded.schemaVersion == EquipmentItemStateCodec.CurrentSchemaVersion,
            "Equipment component schema was not upgraded to V2.");
        Require(EquipmentItemStateCodec.TryDecode(
                encoded,
                out CombatEquipmentInstance restored,
                out string error),
            error);
        Require(restored.instanceId == source.instanceId
                && restored.definitionId == source.definitionId
                && restored.materialId == source.materialId
                && restored.quality == source.quality
                && Mathf.Approximately(restored.durabilityRatio, source.durabilityRatio)
                && restored.loadedAmmo == source.loadedAmmo
                && restored.worldState == source.worldState
                && restored.ownerCharacterId == source.ownerCharacterId
                && restored.sourceStackId == source.sourceStackId
                && restored.moduleSlots.Count == 1
                && restored.moduleSlots[0].moduleInstanceId == "module:test-v18",
            "The physical equipment component lost mutable equipment state.");
        Require(EquipmentItemStateCodec.TryDecodeFull(
                encoded,
                out EquipmentPhysicalStatePayload fullState,
                out error)
                && fullState.attachedModules.Count == 1
                && fullState.attachedModules[0].instanceId == attachedModule.instanceId
                && Mathf.Approximately(
                    fullState.attachedModules[0].condition,
                    attachedModule.condition),
            "The physical equipment component lost attached module state.");

        ItemInstanceComponentSaveData stale = encoded.Clone();
        stale.schemaVersion = 1;
        Require(!EquipmentItemStateCodec.TryDecode(stale, out _, out _),
            "Legacy partial equipment components must not be accepted as V18 authority.");

        Debug.Log(
            "V18 EQUIPMENT ITEM STATE PASS: full mutable equipment state round-tripped "
            + "through physical item component schema V2.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
