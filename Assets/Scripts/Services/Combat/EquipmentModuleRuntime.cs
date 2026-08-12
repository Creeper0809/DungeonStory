using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns the lifecycle of expedition equipment modules. Module state lives in the
/// physical item repository; this aggregate only applies validated transitions.
/// </summary>
public sealed class EquipmentModuleRuntime
{
    private const string MaterialTestCouponItemId = "component:material-test-coupon";
    private readonly IItemInstanceRepository itemInstances;
    private readonly ICombatEquipmentCatalog equipmentCatalog;
    private readonly IEquipmentModuleCatalog moduleCatalog;
    private readonly BlueprintResearchRuntime research;
    private readonly CombatEquipmentPhysicalStateWriter physicalState;
    private readonly IEquipmentPhysicalItemGateway physicalItems;
    private readonly IFacilityCapabilityQuery facilities;

    private IDictionary<string, CombatEquipmentInstance> EquipmentInstances =>
        itemInstances.EquipmentInstances;
    private IDictionary<string, EquipmentModuleInstance> Modules =>
        itemInstances.EquipmentModules;

    public EquipmentModuleRuntime(
        IItemInstanceRepository itemInstances,
        ICombatEquipmentCatalog equipmentCatalog,
        IEquipmentModuleCatalog moduleCatalog,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        CombatEquipmentPhysicalStateWriter physicalState,
        IEquipmentPhysicalItemGateway physicalItems,
        IFacilityCapabilityQuery facilities)
    {
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.equipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        this.moduleCatalog = moduleCatalog
            ?? throw new ArgumentNullException(nameof(moduleCatalog));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(EquipmentModuleRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.physicalState = physicalState
            ?? throw new ArgumentNullException(nameof(physicalState));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public IReadOnlyCollection<EquipmentModuleInstance> Snapshots =>
        Modules.Values.Select(module => module.Clone()).ToArray();

    public EquipmentModuleInstance CreateExpeditionModule(
        string definitionId,
        int grade,
        Vector2Int deliveryPosition,
        WorldItemStackState worldState,
        string destinationId,
        bool identified)
    {
        if (!moduleCatalog.TryGet(definitionId, out _))
        {
            throw new KeyNotFoundException(
                $"Unknown equipment module definition '{definitionId}'.");
        }
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (worldState == WorldItemStackState.FacilityBuffer
            && string.IsNullOrWhiteSpace(normalizedDestination))
        {
            throw new ArgumentException(
                "A facility-buffer equipment module requires a destination ID.",
                nameof(destinationId));
        }

        int safeGrade = Mathf.Clamp(grade, 1, 4);
        EquipmentModuleInstance created = new EquipmentModuleInstance
        {
            instanceId = itemInstances.AllocateItemInstanceId().Value,
            definitionId = definitionId.Trim(),
            grade = safeGrade,
            condition = identified ? Mathf.Clamp01(0.5f + safeGrade * 0.05f) : 0.5f,
            identified = identified,
            runeTuned = false,
            state = identified
                ? EquipmentModuleProcessState.IdentifiedDamaged
                : EquipmentModuleProcessState.Unidentified
        };
        Modules.Add(created.instanceId, created);
        if (!physicalItems.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipmentModule(),
                (ItemInstanceId)created.instanceId,
                deliveryPosition,
                worldState,
                normalizedDestination,
                out string stackId))
        {
            Modules.Remove(created.instanceId);
            throw new InvalidOperationException(
                $"Failed to materialize equipment module '{created.instanceId}'.");
        }
        created.sourceStackId = stackId;
        if (!PersistModulePhysicalState(created))
        {
            physicalItems.TryAbsorbUniqueItemStack(
                stackId,
                (ItemInstanceId)created.instanceId);
            Modules.Remove(created.instanceId);
            throw new InvalidOperationException(
                $"Failed to persist equipment module '{created.instanceId}'.");
        }
        return created.Clone();
    }

    public bool TryAppraise(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.Appraisal,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:relic-appraisal"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:relic-appraisal",
                "facility:equipment:appraisal-bench");
            return false;
        }
        if (!Modules.TryGetValue(moduleInstanceId?.Trim() ?? string.Empty,
                out EquipmentModuleInstance module)
            || module.state != EquipmentModuleProcessState.Unidentified)
        {
            failure = new DomainFailure(FailureCode.ModuleNotUnidentified);
            return false;
        }
        if (!IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        if (!TryPrepareAppraisalSupplies(
                facility,
                destinationId,
                out WorldItemStackSnapshot gauge,
                out WorldItemStackSnapshot lens,
                out failure))
        {
            return false;
        }

        if (!physicalItems.TryConsumeFacilityItemBuffer(
                destinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [MaterialTestCouponItemId] = 1
                },
                out _))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        EquipmentModuleInstance previous = module.Clone();
        module.identified = true;
        module.state = EquipmentModuleProcessState.IdentifiedDamaged;
        if (!PersistModulePhysicalState(module))
        {
            RestoreModuleState(module, previous);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        WearAppraisalTool(gauge, 1f);
        WearAppraisalTool(lens, 2f);
        failure = DomainFailure.None;
        return true;
    }

    private bool TryPrepareAppraisalSupplies(
        BuildableObject facility,
        string destinationId,
        out WorldItemStackSnapshot gauge,
        out WorldItemStackSnapshot lens,
        out DomainFailure failure)
    {
        gauge = FindUsableTool(destinationId, DurableToolItemRules.InspectionGauge);
        lens = FindUsableTool(destinationId, DurableToolItemRules.RuneIdentificationLens);
        bool hasCoupon = physicalItems.GetAllStacks().Any(stack =>
            stack != null
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
            && string.Equals(stack.ItemId, MaterialTestCouponItemId, StringComparison.Ordinal)
            && stack.Quantity > 0);
        if (gauge != null && lens != null && hasCoupon)
        {
            failure = DomainFailure.None;
            return true;
        }

        RequestMissingAppraisalSupply(
            facility,
            destinationId,
            MaterialTestCouponItemId,
            hasCoupon);
        RequestMissingAppraisalSupply(
            facility,
            destinationId,
            DurableToolItemRules.InspectionGauge,
            gauge != null);
        RequestMissingAppraisalSupply(
            facility,
            destinationId,
            DurableToolItemRules.RuneIdentificationLens,
            lens != null);
        failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
        return false;
    }

    private void RequestMissingAppraisalSupply(
        BuildableObject facility,
        string destinationId,
        string itemId,
        bool available)
    {
        if (available || physicalItems.GetAllStacks().Any(stack =>
                stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)))
        {
            return;
        }

        physicalItems.TryRequestItemDelivery(
            itemId,
            1,
            facility.centerPos,
            destinationId,
            out _,
            out _);
    }

    private WorldItemStackSnapshot FindUsableTool(string destinationId, string itemId)
    {
        return physicalItems.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && DurableToolItemRules.ReadCurrentDurability(
                    stack.ItemId,
                    stack.Components) > 0f)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private void WearAppraisalTool(WorldItemStackSnapshot tool, float wear)
    {
        float current = DurableToolItemRules.ReadCurrentDurability(
            tool.ItemId,
            tool.Components);
        if (!physicalItems.TrySetInstanceComponent(
                tool.StackId,
                DurableToolItemRules.CreateDurability(tool.ItemId, current - wear)))
        {
            throw new InvalidOperationException(
                $"Validated appraisal tool '{tool.StackId}' disappeared during appraisal.");
        }
    }

    public bool TryRestore(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.Restoration,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:relic-appraisal"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:relic-appraisal",
                "facility:equipment:restoration-bench");
            return false;
        }
        if (!Modules.TryGetValue(moduleInstanceId?.Trim() ?? string.Empty,
                out EquipmentModuleInstance module)
            || !module.identified
            || !string.IsNullOrWhiteSpace(module.attachedEquipmentInstanceId)
            || module.state == EquipmentModuleProcessState.Lost)
        {
            failure = new DomainFailure(FailureCode.ModuleNotRestorable);
            return false;
        }
        if (!IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        EquipmentModuleInstance previous = module.Clone();
        module.condition = 1f;
        module.state = module.runeTuned
            ? EquipmentModuleProcessState.Tuned
            : EquipmentModuleProcessState.Restored;
        if (!PersistModulePhysicalState(module))
        {
            RestoreModuleState(module, previous);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryTune(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.RuneTuning,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:rune-module-tuning"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:rune-module-tuning",
                "facility:equipment:rune-tuning-room");
            return false;
        }
        if (facilities.FindOperational(
                ResearchFacilityCommandKind.ResonanceTuning).Count == 0)
        {
            failure = new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                "facility:resonance-tuning");
            return false;
        }
        if (!Modules.TryGetValue(moduleInstanceId?.Trim() ?? string.Empty,
                out EquipmentModuleInstance module)
            || module.grade != 4
            || module.state != EquipmentModuleProcessState.Restored
            || module.condition < 0.75f)
        {
            failure = new DomainFailure(FailureCode.ModuleNotTunable);
            return false;
        }
        if (!IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        EquipmentModuleInstance previous = module.Clone();
        module.runeTuned = true;
        module.state = EquipmentModuleProcessState.Tuned;
        if (!PersistModulePhysicalState(module))
        {
            RestoreModuleState(module, previous);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryInstall(
        string equipmentInstanceId,
        string moduleInstanceId,
        int slotIndex,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.PrecisionFitting,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:precision-fitting"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:precision-fitting",
                "facility:equipment:precision-fitting-bench");
            return false;
        }
        if (!EquipmentInstances.TryGetValue(
                equipmentInstanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance equipment)
            || !equipmentCatalog.TryGet(equipment.definitionId,
                out CombatEquipmentDefinitionSO equipmentDefinition)
            || !Modules.TryGetValue(moduleInstanceId?.Trim() ?? string.Empty,
                out EquipmentModuleInstance module)
            || !moduleCatalog.TryGet(module.definitionId,
                out EquipmentModuleDefinitionSO moduleDefinition))
        {
            failure = new DomainFailure(FailureCode.EquipmentOrModuleMissing);
            return false;
        }
        if (slotIndex < 0 || slotIndex >= equipmentDefinition.ModuleSlotCount)
        {
            failure = new DomainFailure(
                FailureCode.ModuleSlotMissing,
                slotIndex.ToString());
            return false;
        }
        if (moduleDefinition.LineageKind != equipmentDefinition.LineageKind)
        {
            failure = new DomainFailure(FailureCode.ModuleLineageMismatch);
            return false;
        }
        if (!module.identified || module.condition < 0.75f
            || module.state is not EquipmentModuleProcessState.Restored
                and not EquipmentModuleProcessState.Tuned)
        {
            failure = new DomainFailure(FailureCode.ModuleNeedsRestoration);
            return false;
        }
        if (module.grade == 4 && !module.runeTuned)
        {
            failure = new DomainFailure(FailureCode.ModuleNeedsRuneTuning);
            return false;
        }
        if (!string.IsNullOrWhiteSpace(module.attachedEquipmentInstanceId))
        {
            failure = new DomainFailure(FailureCode.ModuleAlreadyAttached);
            return false;
        }
        if (!IsEquipmentInLocalBuffer(equipment, destinationId)
            || !IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentOrModuleMissing);
            return false;
        }

        equipment.moduleSlots ??= new List<EquipmentModuleSlotState>();
        EquipmentModuleSlotState slot = equipment.moduleSlots
            .FirstOrDefault(candidate => candidate != null
                && candidate.slotIndex == slotIndex);
        if (slot == null)
        {
            slot = new EquipmentModuleSlotState { slotIndex = slotIndex };
            equipment.moduleSlots.Add(slot);
        }
        if (!string.IsNullOrWhiteSpace(slot.moduleInstanceId)
            && Modules.TryGetValue(slot.moduleInstanceId,
                out EquipmentModuleInstance replaced))
        {
            EquipmentModuleInstance incomingBefore = module.Clone();
            EquipmentModuleInstance replacedBefore = replaced.Clone();
            if (!TryMaterializeReturnedModule(
                    replaced,
                    facility,
                    destinationId,
                    out string returnedStackId))
            {
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
            if (!physicalItems.TryAbsorbUniqueItemStack(
                    module.sourceStackId,
                    (ItemInstanceId)module.instanceId))
            {
                physicalItems.TryAbsorbUniqueItemStack(
                    returnedStackId,
                    (ItemInstanceId)replaced.instanceId);
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
            replaced.attachedEquipmentInstanceId = string.Empty;
            replaced.condition = Mathf.Min(replaced.condition, 0.7f);
            replaced.state = EquipmentModuleProcessState.IdentifiedDamaged;
            replaced.sourceStackId = returnedStackId;
            if (!PersistModulePhysicalState(replaced))
            {
                physicalItems.TryAbsorbUniqueItemStack(
                    returnedStackId,
                    (ItemInstanceId)replaced.instanceId);
                if (!physicalItems.SpawnExistingUniqueItemAt(
                        PhysicalItemIds.ForEquipmentModule(),
                        (ItemInstanceId)module.instanceId,
                        facility.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        destinationId,
                        out string restoredIncomingStackId))
                {
                    throw new InvalidOperationException(
                        $"Failed to roll back module '{module.instanceId}' after replacement persistence failed.");
                }
                RestoreModuleState(replaced, replacedBefore);
                RestoreModuleState(module, incomingBefore);
                module.sourceStackId = restoredIncomingStackId;
                if (!PersistModulePhysicalState(module))
                {
                    throw new InvalidOperationException(
                        $"Failed to persist rolled-back module '{module.instanceId}'.");
                }
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
        }
        else if (!physicalItems.TryAbsorbUniqueItemStack(
                     module.sourceStackId,
                     (ItemInstanceId)module.instanceId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        slot.moduleInstanceId = module.instanceId;
        module.sourceStackId = string.Empty;
        module.attachedEquipmentInstanceId = equipment.instanceId;
        module.state = EquipmentModuleProcessState.Installed;
        physicalState.Persist(equipment);
        failure = DomainFailure.None;
        return true;
    }

    public bool TryRemove(
        string equipmentInstanceId,
        int slotIndex,
        BuildableObject facility,
        out EquipmentModuleInstance removed,
        out DomainFailure failure)
    {
        removed = null;
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.PrecisionFitting,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!EquipmentInstances.TryGetValue(
                equipmentInstanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance equipment))
        {
            failure = new DomainFailure(FailureCode.EquipmentInstanceMissing);
            return false;
        }
        if (!IsEquipmentInLocalBuffer(equipment, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentInstanceMissing);
            return false;
        }

        EquipmentModuleSlotState slot = equipment.moduleSlots?
            .FirstOrDefault(candidate => candidate != null
                && candidate.slotIndex == slotIndex);
        if (slot == null || string.IsNullOrWhiteSpace(slot.moduleInstanceId)
            || !Modules.TryGetValue(slot.moduleInstanceId,
                out EquipmentModuleInstance module))
        {
            failure = new DomainFailure(
                FailureCode.ModuleSlotEmpty,
                slotIndex.ToString());
            return false;
        }
        if (!TryMaterializeReturnedModule(
                module,
                facility,
                destinationId,
                out string returnedStackId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        EquipmentModuleInstance previous = module.Clone();
        module.sourceStackId = returnedStackId;
        module.attachedEquipmentInstanceId = string.Empty;
        module.condition = Mathf.Min(module.condition, 0.7f);
        module.state = EquipmentModuleProcessState.IdentifiedDamaged;
        if (!PersistModulePhysicalState(module))
        {
            physicalItems.TryAbsorbUniqueItemStack(
                returnedStackId,
                (ItemInstanceId)module.instanceId);
            RestoreModuleState(module, previous);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        slot.moduleInstanceId = string.Empty;
        removed = module.Clone();
        physicalState.Persist(equipment);
        failure = DomainFailure.None;
        return true;
    }

    private bool HasCompletedResearch(string researchId)
    {
        return string.IsNullOrWhiteSpace(researchId)
            || research.State.Projects.IsCompleted(new ResearchProjectId(researchId));
    }

    private static bool TryRequireFacility(
        BuildableObject facility,
        string requiredTag,
        out string destinationId,
        out DomainFailure failure)
    {
        destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(facility);
        if (!EquipmentProgressionFacilityContract.Matches(
                facility,
                requiredTag))
        {
            failure = new DomainFailure(
                FailureCode.EquipmentProgressionFacilityUnavailable);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    private bool IsModuleInLocalBuffer(
        EquipmentModuleInstance module,
        string destinationId)
    {
        return module != null
            && !string.IsNullOrWhiteSpace(module.sourceStackId)
            && HasLocalStack(
                module.sourceStackId,
                module.instanceId,
                PhysicalItemIds.ForEquipmentModule(),
                destinationId);
    }

    private bool IsEquipmentInLocalBuffer(
        CombatEquipmentInstance equipment,
        string destinationId)
    {
        return equipment != null
            && !string.IsNullOrWhiteSpace(equipment.sourceStackId)
            && HasLocalStack(
                equipment.sourceStackId,
                equipment.instanceId,
                PhysicalItemIds.ForEquipment(equipment.definitionId),
                destinationId);
    }

    private bool HasLocalStack(
        string stackId,
        string instanceId,
        string itemId,
        string destinationId)
    {
        return physicalItems.GetAllStacks().Any(stack => stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && stack.AvailableQuantity > 0
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(stack.StackId, stackId, StringComparison.Ordinal)
            && string.Equals(
                stack.ItemInstanceId,
                instanceId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.ItemId,
                itemId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal));
    }

    private bool TryMaterializeReturnedModule(
        EquipmentModuleInstance module,
        BuildableObject facility,
        string destinationId,
        out string stackId)
    {
        stackId = string.Empty;
        return module != null
            && string.IsNullOrWhiteSpace(module.sourceStackId)
            && physicalItems.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipmentModule(),
                (ItemInstanceId)module.instanceId,
                facility.centerPos,
                WorldItemStackState.FacilityBuffer,
                destinationId,
                out stackId);
    }

    private bool PersistModulePhysicalState(EquipmentModuleInstance module)
    {
        if (module != null && !string.IsNullOrWhiteSpace(module.sourceStackId))
        {
            return physicalItems.TrySetInstanceComponent(
                module.sourceStackId,
                EquipmentModuleItemStateCodec.Encode(module));
        }
        return false;
    }

    private static void RestoreModuleState(
        EquipmentModuleInstance target,
        EquipmentModuleInstance source)
    {
        target.definitionId = source.definitionId;
        target.grade = source.grade;
        target.condition = source.condition;
        target.identified = source.identified;
        target.runeTuned = source.runeTuned;
        target.state = source.state;
        target.sourceStackId = source.sourceStackId;
        target.attachedEquipmentInstanceId =
            source.attachedEquipmentInstanceId;
    }

}
