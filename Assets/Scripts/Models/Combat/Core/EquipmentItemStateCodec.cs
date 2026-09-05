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

        List<EquipmentModuleInstance> moduleSnapshots =
            (attachedModules ?? Array.Empty<EquipmentModuleInstance>())
            .Where(module => module != null)
            .Select(module => module.Clone())
            .ToList();
        foreach (EquipmentModuleInstance module in moduleSnapshots)
        {
            if (!TryValidateAttachedModule(
                    instance.instanceId,
                    module,
                    out string moduleError))
            {
                throw new ArgumentException(
                    $"Attached module '{module.instanceId}' is invalid: {moduleError}",
                    nameof(attachedModules));
            }
        }

        EquipmentPhysicalStatePayload snapshot = new()
        {
            equipment = instance.Clone(),
            attachedModules = moduleSnapshots
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
            if (!Enum.IsDefined(
                    typeof(CombatEquipmentWorldState),
                    restored.equipment.worldState)
                || (restored.equipment.worldState
                        == CombatEquipmentWorldState.MarketSalePending
                    && (!string.IsNullOrEmpty(
                            restored.equipment.ownerCharacterId)
                        || string.IsNullOrWhiteSpace(
                            restored.equipment.sourceStackId)
                        || !string.Equals(
                            restored.equipment.sourceStackId,
                            restored.equipment.sourceStackId.Trim(),
                            StringComparison.Ordinal)
                        || (restored.equipment.moduleSlots
                                ?? new List<EquipmentModuleSlotState>())
                            .Any(slot => slot != null
                                && !string.IsNullOrWhiteSpace(
                                    slot.moduleInstanceId)))))
            {
                error = "Equipment market-sale custody is invalid.";
                return false;
            }
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
            foreach (EquipmentModuleInstance module in restored.attachedModules)
            {
                if (!TryValidateAttachedModule(
                        restored.equipment.instanceId,
                        module,
                        out string moduleError))
                {
                    error = $"Attached equipment-module state is invalid: {moduleError}";
                    return false;
                }
            }
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

    private static bool TryValidateAttachedModule(
        string equipmentInstanceId,
        EquipmentModuleInstance module,
        out string error)
    {
        if (module == null
            || !((ItemInstanceId)module.instanceId).IsValid
            || string.IsNullOrWhiteSpace(module.definitionId)
            || module.state != EquipmentModuleProcessState.Installed
            || !string.IsNullOrWhiteSpace(module.sourceStackId)
            || !string.Equals(
                module.attachedEquipmentInstanceId,
                equipmentInstanceId,
                StringComparison.Ordinal))
        {
            error = "The attached module has invalid identity, ownership, or process state.";
            return false;
        }

        if (!EquipmentModuleItemStateCodec.TryValidateAppraisalState(
                module,
                out error))
        {
            return false;
        }

        if ((EquipmentModuleAppraisalCommitPhase)module.pendingAppraisal.phase
            != EquipmentModuleAppraisalCommitPhase.None)
        {
            error = "An attached module cannot own a pending appraisal operation.";
            return false;
        }

        error = string.Empty;
        return true;
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
    public const int CurrentSchemaVersion = 2;
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
            string appraisalError = string.Empty;
            bool appraisalValid = restored != null
                && TryValidateAppraisalState(restored, out appraisalError);
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
                    or EquipmentModuleProcessState.Lost
                || !appraisalValid)
            {
                error = string.IsNullOrEmpty(appraisalError)
                    ? "Equipment-module item-state payload has invalid physical identity or state."
                    : appraisalError;
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

    public static bool TryValidateAppraisalState(
        EquipmentModuleInstance module,
        out string error)
    {
        error = string.Empty;
        if (module.nextAppraisalOperationSequence <= 0
            || module.pendingAppraisal == null)
        {
            error = "Equipment-module appraisal sequence or pending state is invalid.";
            return false;
        }

        EquipmentModuleAppraisalCommitSaveData pending = module.pendingAppraisal;
        EquipmentModuleAppraisalCommitPhase phase =
            (EquipmentModuleAppraisalCommitPhase)pending.phase;
        if (phase == EquipmentModuleAppraisalCommitPhase.None)
        {
            bool empty = pending.operationSequence == 0
                && IsEmpty(pending.operationId)
                && IsEmpty(pending.reasonCode)
                && IsEmpty(pending.moduleInstanceId)
                && IsEmpty(pending.destinationId)
                && IsEmpty(pending.couponStackId)
                && IsEmpty(pending.couponItemId)
                && pending.quantity == 0
                && !pending.moduleIdentifiedBefore
                && !pending.moduleIdentifiedAfter
                && pending.moduleStateBefore == EquipmentModuleProcessState.Unidentified
                && pending.moduleStateAfter == EquipmentModuleProcessState.Unidentified
                && IsEmpty(pending.gaugeStackId)
                && IsEmpty(pending.gaugeItemId)
                && Approximately(pending.gaugeDurabilityBefore, 0f)
                && Approximately(pending.gaugeDurabilityAfter, 0f)
                && IsEmpty(pending.lensStackId)
                && IsEmpty(pending.lensItemId)
                && Approximately(pending.lensDurabilityBefore, 0f)
                && Approximately(pending.lensDurabilityAfter, 0f)
                && (pending.sourceStackIds?.Count ?? 0) == 0
                && pending.inputMassGrams == 0L
                && IsEmpty(pending.commitId);
            if (!empty)
            {
                error = "Equipment-module empty appraisal state contains stale provenance.";
            }
            return empty;
        }

        bool common = phase is EquipmentModuleAppraisalCommitPhase.IntentRecorded
                or EquipmentModuleAppraisalCommitPhase.OutcomePublished
            && pending.operationSequence == module.nextAppraisalOperationSequence
            && pending.operationSequence > 0
            && IsCanonical(pending.operationId)
            && IsCanonical(pending.reasonCode)
            && string.Equals(
                pending.moduleInstanceId,
                module.instanceId,
                StringComparison.Ordinal)
            && IsCanonical(pending.destinationId)
            && IsCanonical(pending.couponStackId)
            && IsCanonical(pending.couponItemId)
            && pending.quantity == 1
            && !pending.moduleIdentifiedBefore
            && pending.moduleIdentifiedAfter
            && pending.moduleStateBefore == EquipmentModuleProcessState.Unidentified
            && pending.moduleStateAfter == EquipmentModuleProcessState.IdentifiedDamaged
            && IsCanonical(pending.gaugeStackId)
            && IsCanonical(pending.gaugeItemId)
            && IsCanonical(pending.lensStackId)
            && IsCanonical(pending.lensItemId)
            && !string.Equals(
                pending.gaugeStackId,
                pending.lensStackId,
                StringComparison.Ordinal)
            && IsFiniteNonNegative(pending.gaugeDurabilityBefore)
            && IsFiniteNonNegative(pending.gaugeDurabilityAfter)
            && pending.gaugeDurabilityAfter < pending.gaugeDurabilityBefore
            && IsFiniteNonNegative(pending.lensDurabilityBefore)
            && IsFiniteNonNegative(pending.lensDurabilityAfter)
            && pending.lensDurabilityAfter < pending.lensDurabilityBefore
            && pending.sourceStackIds != null;
        if (!common)
        {
            error = "Equipment-module appraisal contract is invalid.";
            return false;
        }

        if (phase == EquipmentModuleAppraisalCommitPhase.IntentRecorded)
        {
            bool validIntent = pending.sourceStackIds.Count == 0
                && pending.inputMassGrams == 0L
                && IsEmpty(pending.commitId);
            if (!validIntent)
            {
                error = "Equipment-module appraisal intent contains outcome provenance.";
            }
            return validIntent;
        }

        bool validOutcome = pending.sourceStackIds.Count > 0
            && pending.sourceStackIds.All(IsCanonical)
            && pending.sourceStackIds.SequenceEqual(
                pending.sourceStackIds.OrderBy(value => value, StringComparer.Ordinal))
            && pending.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == pending.sourceStackIds.Count
            && pending.inputMassGrams > 0L
            && IsCanonical(pending.commitId);
        if (!validOutcome)
        {
            error = "Equipment-module appraisal outcome provenance is invalid.";
        }
        return validOutcome;
    }

    private static bool IsEmpty(string value) => string.IsNullOrEmpty(value);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;
}
