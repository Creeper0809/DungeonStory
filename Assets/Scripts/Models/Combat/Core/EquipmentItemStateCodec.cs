using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public static class EquipmentItemStateCodec
{
    public const int CurrentSchemaVersion = 3;
    private const string StateJsonKey = "state-json";

    public static ItemInstanceComponentSaveData Encode(
        CombatEquipmentInstance instance,
        IEnumerable<EquipmentModuleInstance> attachedModules = null)
    {
        if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId))
        {
            throw new ArgumentException(
                "A persistent combat-equipment instance is required.",
                nameof(instance));
        }

        EquipmentPhysicalStatePayload snapshot = new()
        {
            equipment = instance.Clone(),
            attachedModules = (attachedModules ?? Array.Empty<EquipmentModuleInstance>())
                .Where(module => module != null)
                .Select(module => module.Clone())
                .ToList()
        };
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Equipment,
            schemaVersion = CurrentSchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new ItemStateValueSaveData
                {
                    key = StateJsonKey,
                    kind = ItemStateValueKind.String,
                    stringValue = JsonUtility.ToJson(snapshot)
                }
            }
        };
    }

    public static bool TryDecode(
        ItemInstanceComponentSaveData component,
        out CombatEquipmentInstance instance,
        out string error)
    {
        if (TryDecodeFull(component, out EquipmentPhysicalStatePayload payload, out error))
        {
            instance = payload.equipment.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public static bool TryDecodeFull(
        ItemInstanceComponentSaveData component,
        out EquipmentPhysicalStatePayload payload,
        out string error)
    {
        payload = null;
        if (component == null
            || !string.Equals(
                component.componentTypeId,
                ItemInstanceComponentIds.Equipment,
                StringComparison.Ordinal))
        {
            error = "The item component is not combat-equipment state.";
            return false;
        }
        if (component.schemaVersion != CurrentSchemaVersion)
        {
            error = $"Unsupported equipment item-state schema V{component.schemaVersion}.";
            return false;
        }

        string json = component.values?
            .FirstOrDefault(value => value != null
                && string.Equals(value.key, StateJsonKey, StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String)?
            .stringValue;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Equipment item-state has no state payload.";
            return false;
        }

        try
        {
            EquipmentPhysicalStatePayload restored =
                JsonUtility.FromJson<EquipmentPhysicalStatePayload>(json);
            if (restored?.equipment == null
                || string.IsNullOrWhiteSpace(restored.equipment.instanceId)
                || string.IsNullOrWhiteSpace(restored.equipment.definitionId))
            {
                error = "Equipment item-state payload has no persistent identity.";
                return false;
            }

            restored.equipment.loadedAmmunition ??=
                new LoadedAmmunitionBatch();
            restored.equipment.powerCharge = Mathf.Clamp(
                restored.equipment.powerCharge,
                0f,
                100f);
            if (restored.equipment.loadedAmmunition.remaining <= 0)
            {
                restored.equipment.loadedAmmunition.Clear();
            }
            else if (string.IsNullOrWhiteSpace(
                         restored.equipment.loadedAmmunition.ammunitionItemId))
            {
                error = "Loaded ammunition has quantity but no physical ammunition item ID.";
                return false;
            }

            restored.attachedModules ??= new List<EquipmentModuleInstance>();
            payload = restored;
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Equipment item-state payload is invalid: {exception.Message}";
            return false;
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EquipmentPhysicalStatePayload
{
    public CombatEquipmentInstance equipment;
    public List<EquipmentModuleInstance> attachedModules = new();
}

public static class EquipmentModuleItemStateCodec
{
    public const int CurrentSchemaVersion = 1;
    private const string StateJsonKey = "state-json";

    public static ItemInstanceComponentSaveData Encode(
        EquipmentModuleInstance instance)
    {
        if (instance == null
            || !((ItemInstanceId)instance.instanceId).IsValid
            || string.IsNullOrWhiteSpace(instance.definitionId)
            || string.IsNullOrWhiteSpace(instance.sourceStackId)
            || !string.IsNullOrWhiteSpace(
                instance.attachedEquipmentInstanceId))
        {
            throw new ArgumentException(
                "A persistent unattached equipment-module instance with a physical stack is required.",
                nameof(instance));
        }

        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.EquipmentModule,
            schemaVersion = CurrentSchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new ItemStateValueSaveData
                {
                    key = StateJsonKey,
                    kind = ItemStateValueKind.String,
                    stringValue = JsonUtility.ToJson(instance.Clone())
                }
            }
        };
    }

    public static bool TryDecode(
        ItemInstanceComponentSaveData component,
        out EquipmentModuleInstance instance,
        out string error)
    {
        instance = null;
        if (component == null
            || !string.Equals(
                component.componentTypeId,
                ItemInstanceComponentIds.EquipmentModule,
                StringComparison.Ordinal))
        {
            error = "The item component is not equipment-module state.";
            return false;
        }
        if (component.schemaVersion != CurrentSchemaVersion)
        {
            error = $"Unsupported equipment-module item-state schema V{component.schemaVersion}.";
            return false;
        }

        string json = component.values?
            .FirstOrDefault(value => value != null
                && string.Equals(value.key, StateJsonKey, StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String)?
            .stringValue;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Equipment-module item-state has no state payload.";
            return false;
        }

        try
        {
            EquipmentModuleInstance restored =
                JsonUtility.FromJson<EquipmentModuleInstance>(json);
            if (restored == null
                || !((ItemInstanceId)restored.instanceId).IsValid
                || string.IsNullOrWhiteSpace(restored.definitionId)
                || string.IsNullOrWhiteSpace(restored.sourceStackId)
                || !string.IsNullOrWhiteSpace(
                    restored.attachedEquipmentInstanceId)
                || !Enum.IsDefined(
                    typeof(EquipmentModuleProcessState),
                    restored.state)
                || restored.state is EquipmentModuleProcessState.Installed
                    or EquipmentModuleProcessState.Lost)
            {
                error = "Equipment-module item-state payload has invalid physical identity or state.";
                return false;
            }

            instance = restored.Clone();
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Equipment-module item-state payload is invalid: {exception.Message}";
            return false;
        }
    }
}
