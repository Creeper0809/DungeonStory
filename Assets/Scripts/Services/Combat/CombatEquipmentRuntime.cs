using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CombatEquipmentRuntime :
    ICombatEquipmentRuntime,
    ICombatLoadoutRuntime,
    ICombatEquipmentBurdenQuery
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IItemInstanceRepository itemInstances;
    private readonly ICharacterCarryInventoryRegistry carryInventories;
    private readonly CombatEquipmentStatProjector statProjector;
    private readonly IEquipmentModuleCatalog moduleCatalog;
    private readonly CombatEquipmentCraftingRuntime crafting;
    private readonly EquipmentModuleRuntime moduleRuntime;
    private readonly EquipmentHistoryTransferRuntime historyRuntime;
    private readonly CombatEquipmentPhysicalStateWriter physicalState;
    private readonly CombatEquipmentLoadoutRuntime loadoutRuntime;
    private readonly CombatEquipmentRuntimeStateStore stateStore;
    private readonly IEquipmentPhysicalItemGateway itemStackRuntime;
    private IDictionary<string, CombatEquipmentInstance> instances =>
        itemInstances.EquipmentInstances;
    private IDictionary<string, EquipmentModuleInstance> moduleInstances =>
        itemInstances.EquipmentModules;

    public CombatEquipmentRuntime(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository itemInstances,
        ICharacterCarryInventoryRegistry carryInventories,
        IEquipmentModuleCatalog moduleCatalog,
        IEquipmentPhysicalItemGateway itemStackRuntime,
        CombatEquipmentRuntimeCollaborators collaborators,
        CombatEquipmentCraftingRuntime crafting,
        CombatEquipmentLoadoutRuntime loadoutRuntime)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.carryInventories = carryInventories
            ?? throw new ArgumentNullException(nameof(carryInventories));
        this.moduleCatalog = moduleCatalog
            ?? throw new ArgumentNullException(nameof(moduleCatalog));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        CombatEquipmentRuntimeCollaborators requiredCollaborators = collaborators
            ?? throw new ArgumentNullException(nameof(collaborators));
        this.statProjector = requiredCollaborators.StatProjector;
        this.physicalState = requiredCollaborators.PhysicalState;
        this.moduleRuntime = requiredCollaborators.ModuleRuntime;
        this.historyRuntime = requiredCollaborators.HistoryRuntime;
        this.stateStore = requiredCollaborators.StateStore;
        this.crafting = crafting
            ?? throw new ArgumentNullException(nameof(crafting));
        this.loadoutRuntime = loadoutRuntime
            ?? throw new ArgumentNullException(nameof(loadoutRuntime));
    }

    public IReadOnlyList<CombatEquipmentDefinitionSO> Definitions => catalog.All;
    public IReadOnlyCollection<CombatEquipmentInstance> Instances =>
        instances.Values.Select(instance => instance.Clone()).ToArray();
    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue => crafting.Queue;
    public IReadOnlyCollection<EquipmentModuleInstance> ModuleInstances =>
        moduleRuntime.Snapshots;
    public IReadOnlyList<EquipmentHistoryTransferOrder> HistoryTransferOrders =>
        historyRuntime.Snapshots;

    public bool TryGetModuleDefinition(
        string definitionId,
        out EquipmentModuleDefinitionSO definition)
    {
        return moduleCatalog.TryGet(
            definitionId?.Trim() ?? string.Empty,
            out definition);
    }

    public bool TryGetDefinition(
        string definitionId,
        out CombatEquipmentDefinitionSO definition)
    {
        return catalog.TryGet(definitionId, out definition);
    }

    public bool IsDefinitionUnlocked(string definitionId, out string failureReason)
    {
        return crafting.IsDefinitionUnlocked(definitionId, out failureReason);
    }

    public int GetAvailableCount(string definitionId)
    {
        string normalizedId = definitionId?.Trim() ?? string.Empty;
        return instances.Values.Count(instance =>
            instance != null
            && string.Equals(instance.definitionId, normalizedId, StringComparison.Ordinal)
            && instance.worldState == CombatEquipmentWorldState.Stored);
    }

    public IReadOnlyList<CraftMaterialDefinitionSO> GetAllowedMaterials(
        string definitionId)
    {
        return crafting.GetAllowedMaterials(definitionId);
    }

    public CombatEquipmentCraftMaterialPolicySaveData GetCraftMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility)
    {
        return crafting.GetMaterialPolicy(definitionId, craftingFacility);
    }

    public bool SetCraftMaterialAllowed(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        bool allowed,
        out string failureReason)
    {
        return crafting.SetMaterialAllowed(
            definitionId,
            materialId,
            craftingFacility,
            allowed,
            out failureReason);
    }

    public bool MoveCraftMaterialPriority(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        int offset,
        out string failureReason)
    {
        return crafting.MoveMaterialPriority(
            definitionId,
            materialId,
            craftingFacility,
            offset,
            out failureReason);
    }

    public bool TryGetDerivedStats(
        string instanceId,
        out CombatEquipmentDerivedStats stats)
    {
        stats = default;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            return false;
        }

        CraftMaterialDefinitionSO material = crafting.ResolveInstanceMaterial(
            instance,
            definition);
        stats = statProjector.Build(definition, material, instance);
        return true;
    }

    public bool TryGetPreviewStats(
        string definitionId,
        string materialId,
        out CombatEquipmentDerivedStats stats)
    {
        return crafting.TryGetPreviewStats(definitionId, materialId, out stats);
    }

    public bool TrySalvage(
        string instanceId,
        Vector2Int outputPosition,
        out string recoveredItemId,
        out int recoveredAmount,
        out string failureReason) =>
        TrySalvage(
            instanceId,
            worker: null,
            outputPosition,
            out recoveredItemId,
            out recoveredAmount,
            out failureReason);

    public bool TrySalvage(
        string instanceId,
        CharacterActor worker,
        Vector2Int outputPosition,
        out string recoveredItemId,
        out int recoveredAmount,
        out string failureReason)
    {
        recoveredItemId = string.Empty;
        recoveredAmount = 0;
        failureReason = string.Empty;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "해체할 장비를 찾을 수 없습니다.";
            return false;
        }

        if (instance.worldState is CombatEquipmentWorldState.Equipped
            or CombatEquipmentWorldState.ExpeditionPacked
            or CombatEquipmentWorldState.MaintenanceBuffer
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
        {
            failureReason = "장착·출정·수리 중인 장비는 해체할 수 없습니다.";
            return false;
        }

        CraftMaterialDefinitionSO material = crafting.ResolveInstanceMaterial(
            instance,
            definition);
        if (material == null || string.IsNullOrWhiteSpace(material.ItemId))
        {
            failureReason = "원래 재질을 확인할 수 없습니다.";
            return false;
        }

        recoveredAmount = Mathf.FloorToInt(
            definition.PrimaryMaterialAmount
            * 0.5f
            * Mathf.Clamp01(instance.durabilityRatio)
            * (worker != null
                ? Mathf.Max(
                    0f,
                    worker.GetDetailedStatMultiplier(
                        GameplayEffectTargetIds.SalvageYield))
                : 1f));
        if (recoveredAmount <= 0)
        {
            failureReason = "회수할 수 있는 재료가 남아 있지 않습니다.";
            return false;
        }

        if (!itemStackRuntime.SpawnItemAt(
                material.ItemId,
                recoveredAmount,
                outputPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned != recoveredAmount)
        {
            recoveredAmount = 0;
            failureReason = "해체 재료를 월드에 생성하지 못했습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            itemStackRuntime.DeleteStack(instance.sourceStackId);
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instances.Remove(instance.instanceId);
        recoveredItemId = material.ItemId;
        return true;
    }

    public bool TryDiscardBySourceStack(
        string sourceStackId,
        out bool wasSalvageable,
        out string failureReason)
    {
        wasSalvageable = false;
        failureReason = string.Empty;
        if (!TryGetInstanceBySourceStack(sourceStackId, out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "폐기할 장비 인스턴스를 찾을 수 없습니다.";
            return false;
        }
        if (instance.worldState is CombatEquipmentWorldState.Equipped
            or CombatEquipmentWorldState.ExpeditionPacked
            or CombatEquipmentWorldState.MaintenanceBuffer
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
        {
            failureReason = "장착·출정·수리 중인 장비는 폐기할 수 없습니다.";
            return false;
        }
        wasSalvageable = definition.PrimaryMaterialAmount > 0
            && instance.durabilityRatio > 0f;
        if (!itemStackRuntime.DeleteStack(sourceStackId))
        {
            failureReason = "장비의 물리 아이템을 폐기하지 못했습니다.";
            return false;
        }
        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instances.Remove(instance.instanceId);
        return true;
    }

    public bool TryQueueCraft(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        return crafting.TryQueue(definitionId, craftingFacility, out failureReason);
    }

    public bool TryQueueCraft(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        return crafting.TryQueue(
            definitionId,
            materialId,
            craftingFacility,
            out failureReason);
    }

    public bool HasPendingCraftWork(IEnumerable<string> craftableDefinitionIds)
    {
        return crafting.HasPendingWork(craftableDefinitionIds);
    }

    public bool TryGetNextCraftMaterialContext(
        IEnumerable<string> craftableDefinitionIds,
        CharacterActor worker,
        out string definitionId,
        out string materialId,
        out bool usesSubstituteMaterial) =>
        crafting.TryGetNextCraftMaterialContext(
            craftableDefinitionIds,
            worker,
            out definitionId,
            out materialId,
            out usesSubstituteMaterial);

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId)
    {
        return crafting.ApplyWork(
            craftableDefinitionIds,
            workUnits,
            out completedDefinitionId,
            out _);
    }

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId,
        out string completedMaterialId)
    {
        return crafting.ApplyWork(
            craftableDefinitionIds,
            workUnits,
            out completedDefinitionId,
            out completedMaterialId);
    }

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        CharacterActor worker,
        float relevantSkill,
        out string completedDefinitionId,
        out string completedMaterialId,
        out CombatEquipmentQuality completedQuality)
    {
        return crafting.ApplyWork(
            craftableDefinitionIds,
            workUnits,
            worker,
            relevantSkill,
            out completedDefinitionId,
            out completedMaterialId,
            out completedQuality);
    }

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        CharacterActor worker,
        float relevantSkill,
        out string completedDefinitionId,
        out string completedMaterialId,
        out CombatEquipmentQuality completedQuality,
        out MythicProvenanceSaveData completedMythicProvenance)
    {
        return crafting.ApplyWork(
            craftableDefinitionIds,
            workUnits,
            worker,
            relevantSkill,
            out completedDefinitionId,
            out completedMaterialId,
            out completedQuality,
            out completedMythicProvenance);
    }

    public WorkerSelectionPolicySaveData GetCraftWorkerPolicy(string orderId) =>
        crafting.GetWorkerPolicy(orderId);

    public bool SetCraftWorkerPolicy(
        string orderId,
        WorkerSelectionPolicySaveData policy,
        out string failureReason) =>
        crafting.SetWorkerPolicy(orderId, policy, out failureReason);

    public bool SetCraftQualityTarget(
        string orderId,
        CraftsmanshipQualityTier minimumQuality,
        RejectedOutputDisposition rejectedDisposition,
        QualityRepeatLimitMode repeatLimitMode,
        int maximumAttempts,
        float workBudget,
        int requiredAcceptedCount,
        out string failureReason) =>
        crafting.SetQualityTarget(
            orderId,
            minimumQuality,
            rejectedDisposition,
            repeatLimitMode,
            maximumAttempts,
            workBudget,
            requiredAcceptedCount,
            out failureReason);

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState = CombatEquipmentWorldState.Stored,
        string materialId = "")
    {
        return crafting.CreateInstance(
            definitionId,
            quality,
            worldState,
            materialId);
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId,
        MythicProvenanceSaveData mythicProvenance)
    {
        return crafting.CreateInstance(
            definitionId,
            quality,
            worldState,
            materialId,
            mythicProvenance);
    }

    public CombatEquipmentInstance CreateExternalInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        string materialId = "")
    {
        return crafting.CreateExternalInstance(
            definitionId,
            quality,
            materialId);
    }

    public bool TryGetInstance(string instanceId, out CombatEquipmentInstance instance)
    {
        if (instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance stored))
        {
            instance = stored.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryUpdateEvolutionState(
        string instanceId,
        EquipmentEvolutionState evolutionState)
    {
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
        {
            return false;
        }

        instance.evolution = evolutionState?.Clone()
            ?? new EquipmentEvolutionState();
        CombatEquipmentStatProjector.NormalizeEvolutionPresentationState(instance.evolution);
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryGetInstanceBySourceStack(
        string sourceStackId,
        out CombatEquipmentInstance instance)
    {
        CombatEquipmentInstance stored = instances.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (stored != null)
        {
            instance = stored.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryLinkToWorldStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || string.IsNullOrWhiteSpace(sourceStackId)
            || CombatEquipmentWorldStateRules.IsExternalCustody(instance.worldState)
            || CombatEquipmentWorldStateRules.IsExternalCustody(worldState))
        {
            return false;
        }

        WorldItemStackSnapshot physicalStack = itemStackRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    sourceStackId.Trim(),
                    StringComparison.Ordinal));
        if (physicalStack == null
            || !string.Equals(
                physicalStack.ItemInstanceId,
                instance.instanceId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (worldState is CombatEquipmentWorldState.Stored
            or CombatEquipmentWorldState.Loose
            or CombatEquipmentWorldState.Carried
            or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            loadoutRuntime.RemoveEquipment(instance.instanceId);
            instance.ownerCharacterId = string.Empty;
        }

        instance.sourceStackId = sourceStackId.Trim();
        instance.worldState = worldState;
        PersistPhysicalState(instance);

        return true;
    }

    private void PersistPhysicalState(CombatEquipmentInstance instance)
    {
        physicalState.Persist(instance);
    }

    public bool TrySetWorldStateBySourceStack(
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        CombatEquipmentInstance instance = instances.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (instance == null)
        {
            return false;
        }
        if (CombatEquipmentWorldStateRules.IsExternalCustody(instance.worldState)
            || CombatEquipmentWorldStateRules.IsExternalCustody(worldState))
        {
            return false;
        }

        instance.worldState = worldState;
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryMarkLost(string instanceId)
    {
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
        {
            return false;
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Lost;
        foreach (EquipmentModuleSlotState slot in instance.moduleSlots
                     ?? new List<EquipmentModuleSlotState>())
        {
            if (slot != null
                && moduleInstances.TryGetValue(slot.moduleInstanceId,
                    out EquipmentModuleInstance module))
            {
                module.attachedEquipmentInstanceId = string.Empty;
                module.state = EquipmentModuleProcessState.Lost;
                module.condition = 0f;
            }
        }
        return true;
    }

    public bool TryBeginMarketSale(
        string sourceStackId,
        string operationId,
        out CombatEquipmentInstance pendingInstance,
        out string failureReason)
    {
        pendingInstance = null;
        failureReason = string.Empty;
        string stackId = sourceStackId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        if (stackId.Length == 0
            || operation.Length == 0
            || !string.Equals(stackId, stackId.Trim(), StringComparison.Ordinal)
            || !string.Equals(operation, operation.Trim(), StringComparison.Ordinal))
        {
            failureReason = "market-sale-custody-invalid";
            return false;
        }

        CombatEquipmentInstance instance = instances.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.sourceStackId, stackId, StringComparison.Ordinal));
        if (instance == null)
        {
            failureReason = "market-sale-equipment-missing";
            return false;
        }
        if (instance.worldState == CombatEquipmentWorldState.MarketSalePending)
        {
            if (!string.Equals(
                    instance.sourceStackId,
                    operation,
                    StringComparison.Ordinal))
            {
                failureReason = "market-sale-equipment-operation-conflict";
                return false;
            }
            pendingInstance = instance.Clone();
            return true;
        }

        WorldItemStackSnapshot saleStack = itemStackRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(stack.StackId, stackId, StringComparison.Ordinal));
        if (saleStack == null
            || saleStack.Quantity != 1
            || saleStack.State != WorldItemStackState.FacilityBuffer
            || !string.Equals(
                saleStack.DestinationId,
                QualityRejectedOutputRules.MarketDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                saleStack.ItemInstanceId,
                instance.instanceId,
                StringComparison.Ordinal))
        {
            failureReason = "market-sale-physical-custody-mismatch";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            || instance.worldState is CombatEquipmentWorldState.Equipped
                or CombatEquipmentWorldState.ExpeditionPacked
                or CombatEquipmentWorldState.MaintenanceBuffer
                or CombatEquipmentWorldState.Carried
                or CombatEquipmentWorldState.RetailStock
            || (instance.moduleSlots ?? new List<EquipmentModuleSlotState>())
                .Any(slot => slot != null
                    && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)))
        {
            failureReason = "market-sale-equipment-unavailable";
            return false;
        }

        ItemInstanceComponentSaveData physicalComponent =
            (saleStack.Components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .SingleOrDefault(component => component != null
                && string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Equipment,
                    StringComparison.Ordinal));
        ItemInstanceComponentSaveData expectedComponent =
            EquipmentItemStateCodec.Encode(
                instance,
                (instance.moduleSlots ?? new List<EquipmentModuleSlotState>())
                .Where(slot => slot != null
                    && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)
                    && moduleInstances.ContainsKey(slot.moduleInstanceId))
                .Select(slot => moduleInstances[slot.moduleInstanceId]));
        if (physicalComponent == null
            || !string.Equals(
                physicalComponent.ToCanonicalString(),
                expectedComponent.ToCanonicalString(),
                StringComparison.Ordinal))
        {
            failureReason = "market-sale-equipment-component-drift";
            return false;
        }

        CombatEquipmentInstance frozen = instance.Clone();
        frozen.ownerCharacterId = string.Empty;
        frozen.sourceStackId = operation;
        frozen.worldState = CombatEquipmentWorldState.MarketSalePending;
        if (!itemStackRuntime.TrySetInstanceComponent(
                stackId,
                EquipmentItemStateCodec.Encode(frozen)))
        {
            failureReason = "market-sale-custody-persist-failed";
            return false;
        }
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = operation;
        instance.worldState = CombatEquipmentWorldState.MarketSalePending;
        pendingInstance = instance.Clone();
        return true;
    }

    public bool TryFinalizeMarketSale(
        string itemInstanceId,
        string operationId,
        out CombatEquipmentInstance soldInstance,
        out string failureReason)
    {
        soldInstance = null;
        failureReason = string.Empty;
        string instanceId = itemInstanceId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        if (!instances.TryGetValue(instanceId, out CombatEquipmentInstance instance))
        {
            failureReason = "market-sale-equipment-release-authority-missing";
            return false;
        }
        if (instance.worldState != CombatEquipmentWorldState.MarketSalePending
            || !string.Equals(
                instance.sourceStackId,
                operation,
                StringComparison.Ordinal))
        {
            failureReason = "market-sale-equipment-custody-mismatch";
            return false;
        }
        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instances.Remove(instance.instanceId);
        soldInstance = instance.Clone();
        return true;
    }

    public bool TryRestoreMarketSalePendingToPhysical(
        string itemInstanceId,
        string operationId,
        string sourceStackId,
        CombatEquipmentWorldState restoredWorldState,
        out string failureReason)
    {
        failureReason = string.Empty;
        string instanceId = itemInstanceId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        string stackId = sourceStackId ?? string.Empty;
        if (restoredWorldState is not (
                CombatEquipmentWorldState.Stored
                or CombatEquipmentWorldState.Loose)
            || !instances.TryGetValue(instanceId, out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.MarketSalePending
            || !string.Equals(instance.sourceStackId, operation, StringComparison.Ordinal))
        {
            failureReason = "market-sale-physical-restore-authority-mismatch";
            return false;
        }
        WorldItemStackSnapshot physicalStack = itemStackRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(stack.StackId, stackId, StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemInstanceId,
                    instanceId,
                    StringComparison.Ordinal));
        if (physicalStack == null)
        {
            failureReason = "market-sale-physical-restore-stack-missing";
            return false;
        }

        instance.sourceStackId = stackId;
        instance.ownerCharacterId = string.Empty;
        instance.worldState = restoredWorldState;
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryBindRetailStock(
        string instanceId,
        string retailSourceOperationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedInstanceId = instanceId?.Trim() ?? string.Empty;
        string normalizedOperationId = retailSourceOperationId?.Trim() ?? string.Empty;
        if (normalizedInstanceId.Length == 0
            || normalizedOperationId.Length == 0
            || !string.Equals(instanceId, normalizedInstanceId, StringComparison.Ordinal)
            || !string.Equals(
                retailSourceOperationId,
                normalizedOperationId,
                StringComparison.Ordinal))
        {
            failureReason = "retail-stock-identity-not-canonical";
            return false;
        }
        if (!instances.TryGetValue(
                normalizedInstanceId,
                out CombatEquipmentInstance instance))
        {
            failureReason = "retail-stock-equipment-instance-missing";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            || instance.worldState is CombatEquipmentWorldState.ExpeditionPacked
                or CombatEquipmentWorldState.MaintenanceBuffer
                or CombatEquipmentWorldState.Carried
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
        {
            failureReason = "retail-stock-equipment-owned-by-active-domain";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(instance.sourceStackId)
            && !string.Equals(
                instance.sourceStackId,
                normalizedOperationId,
                StringComparison.Ordinal))
        {
            failureReason = "retail-stock-equipment-source-conflict";
            return false;
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = normalizedOperationId;
        instance.worldState = CombatEquipmentWorldState.RetailStock;
        return true;
    }

    public bool TryBindPhysicalToRetailStock(
        string instanceId,
        string expectedSourceStackId,
        string retailSourceOperationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedInstanceId = instanceId?.Trim() ?? string.Empty;
        string normalizedStackId = expectedSourceStackId?.Trim() ?? string.Empty;
        string normalizedOperationId = retailSourceOperationId?.Trim() ?? string.Empty;
        if (normalizedInstanceId.Length == 0
            || normalizedStackId.Length == 0
            || normalizedOperationId.Length == 0
            || !string.Equals(instanceId, normalizedInstanceId, StringComparison.Ordinal)
            || !string.Equals(expectedSourceStackId, normalizedStackId, StringComparison.Ordinal)
            || !string.Equals(retailSourceOperationId, normalizedOperationId, StringComparison.Ordinal)
            || !instances.TryGetValue(normalizedInstanceId, out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.Carried
            || !string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            || !string.Equals(instance.sourceStackId, normalizedStackId, StringComparison.Ordinal))
        {
            failureReason = "retail-stock-physical-bind-authority-mismatch";
            return false;
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = normalizedOperationId;
        instance.worldState = CombatEquipmentWorldState.RetailStock;
        return true;
    }

    public bool TryConsumeRetailStock(
        string instanceId,
        string retailSourceOperationId,
        out CombatEquipmentInstance consumedInstance,
        out string failureReason)
    {
        consumedInstance = null;
        failureReason = string.Empty;
        string normalizedInstanceId = instanceId?.Trim() ?? string.Empty;
        string normalizedOperationId = retailSourceOperationId?.Trim() ?? string.Empty;
        if (!instances.TryGetValue(
                normalizedInstanceId,
                out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.RetailStock
            || !string.Equals(
                instance.sourceStackId,
                normalizedOperationId,
                StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(instance.ownerCharacterId))
        {
            failureReason = "retail-stock-equipment-authority-mismatch";
            return false;
        }

        string[] attachedModuleIds = (instance.moduleSlots
                ?? new List<EquipmentModuleSlotState>())
            .Where(slot => slot != null
                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId))
            .Select(slot => slot.moduleInstanceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string moduleId in attachedModuleIds)
        {
            if (!moduleInstances.ContainsKey(moduleId))
            {
                failureReason = "retail-stock-attached-module-missing";
                return false;
            }
        }

        consumedInstance = instance.Clone();
        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instances.Remove(instance.instanceId);
        foreach (string moduleId in attachedModuleIds)
        {
            moduleInstances.Remove(moduleId);
        }
        return true;
    }

    public bool TryRestoreRetailStockToPhysical(
        string instanceId,
        string retailSourceOperationId,
        string sourceStackId,
        CombatEquipmentWorldState restoredWorldState,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedInstanceId = instanceId?.Trim() ?? string.Empty;
        string normalizedOperationId = retailSourceOperationId?.Trim() ?? string.Empty;
        string normalizedStackId = sourceStackId?.Trim() ?? string.Empty;
        if (restoredWorldState is not (
                CombatEquipmentWorldState.Stored
                or CombatEquipmentWorldState.Loose
                or CombatEquipmentWorldState.Carried
                or CombatEquipmentWorldState.MaintenanceBuffer)
            || normalizedStackId.Length == 0
            || !instances.TryGetValue(
                normalizedInstanceId,
                out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.RetailStock
            || !string.Equals(
                instance.sourceStackId,
                normalizedOperationId,
                StringComparison.Ordinal))
        {
            failureReason = "retail-stock-physical-restore-authority-mismatch";
            return false;
        }
        WorldItemStackSnapshot physicalStack = itemStackRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    normalizedStackId,
                    StringComparison.Ordinal));
        if (physicalStack == null
            || !string.Equals(
                physicalStack.ItemInstanceId,
                normalizedInstanceId,
                StringComparison.Ordinal))
        {
            failureReason = "retail-stock-physical-restore-stack-missing";
            return false;
        }

        instance.sourceStackId = normalizedStackId;
        instance.ownerCharacterId = string.Empty;
        instance.worldState = restoredWorldState;
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryAssignToCharacter(
        string characterId,
        string instanceId,
        out string failureReason)
    {
        return loadoutRuntime.TryAssign(
            characterId,
            instanceId,
            out failureReason);
    }

    public bool TryUnassignSlot(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        out string failureReason)
    {
        return loadoutRuntime.TryUnassign(characterId, slot, out failureReason);
    }

    public bool TrySetActiveWeapon(
        string characterId,
        string instanceId,
        out string failureReason)
    {
        return loadoutRuntime.TrySetActiveWeapon(
            characterId,
            instanceId,
            out failureReason);
    }

    public bool TrySetActiveProfile(string characterId, string profileId)
    {
        return loadoutRuntime.TrySetActiveProfile(characterId, profileId);
    }

    public bool TrySetFireMode(
        string characterId,
        CombatFireMode fireMode,
        out string failureReason)
    {
        return loadoutRuntime.TrySetFireMode(
            characterId,
            fireMode,
            out failureReason);
    }

    public bool TrySetHoldFire(string characterId, bool holdFire)
    {
        return loadoutRuntime.TrySetHoldFire(characterId, holdFire);
    }

    public CharacterCombatLoadoutState GetOrCreateLoadout(string characterId)
    {
        return loadoutRuntime.GetOrCreate(characterId);
    }

    public CharacterCombatLoadoutProfile GetActiveProfileSnapshot(string characterId)
    {
        return loadoutRuntime.GetActiveProfileSnapshot(characterId);
    }

    public bool TryGetActiveProfileSnapshot(
        string characterId,
        out CharacterCombatLoadoutProfile profile)
    {
        return loadoutRuntime.TryGetActiveProfileSnapshot(
            characterId,
            out profile);
    }

    public bool TryGetActiveWeapon(
        string characterId,
        out CombatWeaponSnapshot weapon)
    {
        return loadoutRuntime.TryGetActiveWeapon(characterId, out weapon);
    }

    public IReadOnlyList<CombatArmorSnapshot> GetArmor(string characterId)
    {
        return loadoutRuntime.GetArmor(characterId);
    }

    public CombatShieldSnapshot GetShield(
        string characterId,
        float incomingAngleDegrees = 0f)
    {
        return loadoutRuntime.GetShield(characterId, incomingAngleDegrees);
    }


    public bool HasCompatibleAmmunition(
        string characterId,
        string instanceId)
    {
        CharacterCarryInventory inventory = carryInventories.Find((CharacterId)characterId);
        if (inventory == null)
        {
            return false;
        }

        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        if (!catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon)
        {
            return false;
        }

        return CombatAmmunitionPolicy.CountAvailable(weapon, inventory) > 0;
    }

    public bool TryGetPreferredAmmunitionItemId(
        string definitionId,
        out ItemDefinitionId itemId)
    {
        itemId = default;
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon)
        {
            return false;
        }

        itemId = CombatAmmunitionPolicy.GetPreferred(
            weapon.CompatibleAmmunitionItemIds);
        return itemId.IsValid;
    }

    public bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out int consumedAmmo)
    {
        return TryReloadFromInventory(
            instanceId,
            inventory,
            out _,
            out consumedAmmo);
    }

    public bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo)
    {
        consumedAmmoItemId = default;
        consumedAmmo = 0;
        if (inventory == null
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon
            || weapon.MagazineCapacity <= 0)
        {
            return false;
        }

        instance.loadedAmmunition ??= new LoadedAmmunitionBatch();
        ItemDefinitionId selectedItemId;
        if (instance.loadedAmmunition.remaining > 0)
        {
            selectedItemId = (ItemDefinitionId)
                instance.loadedAmmunition.ammunitionItemId;
            if (!selectedItemId.IsValid
                || !weapon.CompatibleAmmunitionItemIds.Contains(
                    selectedItemId)
                || inventory.CountItem(selectedItemId.Value) <= 0)
            {
                return false;
            }
        }
        else if (!CombatAmmunitionPolicy.TrySelectAvailable(
                     weapon,
                     inventory,
                     out selectedItemId))
        {
            return false;
        }

        int needed = Mathf.Max(
            0,
            weapon.MagazineCapacity - instance.loadedAmmunition.remaining);
        int available = inventory.CountItem(selectedItemId.Value);
        consumedAmmo = Mathf.Min(needed, available);
        if (consumedAmmo <= 0
            || !CombatAmmunitionPolicy.TryConsumeSelected(
                inventory,
                selectedItemId,
                consumedAmmo))
        {
            consumedAmmo = 0;
            return false;
        }

        consumedAmmoItemId = selectedItemId;
        instance.loadedAmmunition.ammunitionItemId = selectedItemId.Value;
        instance.loadedAmmunition.remaining += consumedAmmo;
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out int consumedAmmo)
    {
        return TryReloadFromCharacterInventory(
            characterId,
            instanceId,
            out _,
            out consumedAmmo);
    }

    public bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo)
    {
        return TryReloadFromInventory(
            instanceId,
            carryInventories.Find((CharacterId)characterId),
            out consumedAmmoItemId,
            out consumedAmmo);
    }

    public bool TryConsumeLoadedAmmo(string instanceId)
    {
        return TryConsumeLoadedAmmo(instanceId, 1);
    }

    public bool TryConsumeLoadedAmmo(string instanceId, int amount)
    {
        int requested = Mathf.Max(1, amount);
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || instance.loadedAmmunition == null
            || instance.loadedAmmunition.remaining < requested
            || string.IsNullOrWhiteSpace(
                instance.loadedAmmunition.ammunitionItemId))
        {
            return false;
        }

        instance.loadedAmmunition.remaining -= requested;
        if (instance.loadedAmmunition.remaining == 0)
        {
            instance.loadedAmmunition.Clear();
        }
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryConsumePower(string instanceId, float amount)
    {
        float requested = Mathf.Max(0f, amount);
        if (requested <= 0f
            || !instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || (CombatEquipmentRoleRules.For(instance.definitionId)
                & CombatEquipmentRoleFlags.Powered) == 0
            || instance.powerCharge <= 0f)
        {
            return false;
        }

        instance.powerCharge = Mathf.Clamp(
            instance.powerCharge - requested,
            0f,
            100f);
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryRestorePower(string instanceId, float amount)
    {
        float restored = Mathf.Max(0f, amount);
        if (restored <= 0f
            || !instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || (CombatEquipmentRoleRules.For(instance.definitionId)
                & CombatEquipmentRoleFlags.Powered) == 0)
        {
            return false;
        }

        instance.powerCharge = Mathf.Clamp(
            instance.powerCharge + restored,
            0f,
            100f);
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryLoadExternalAmmunition(
        string instanceId,
        string ammunitionItemId,
        int amount)
    {
        string normalizedAmmo = ammunitionItemId?.Trim() ?? string.Empty;
        if (amount <= 0
            || !instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon
            || weapon.MagazineCapacity <= 0
            || !weapon.CompatibleAmmunitionItemIds.Contains(
                (ItemDefinitionId)normalizedAmmo))
        {
            return false;
        }

        instance.loadedAmmunition = new LoadedAmmunitionBatch
        {
            ammunitionItemId = normalizedAmmo,
            remaining = Mathf.Min(amount, weapon.MagazineCapacity)
        };
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryApplyDurabilityDamage(string instanceId, float damage)
    {
        if (damage <= 0f
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is CombatEquipmentKind.MeleeWeapon
                or CombatEquipmentKind.RangedWeapon
                or CombatEquipmentKind.RecoverableThrowingWeapon)
        {
            return false;
        }

        float maxDurability = statProjector.Build(
            definition,
            crafting.ResolveInstanceMaterial(instance, definition),
            instance).MaxDurability;
        instance.durabilityRatio = Mathf.Clamp01(
            instance.durabilityRatio - damage / maxDurability);
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryDetachForMaintenance(
        string instanceId,
        out CombatEquipmentInstance detached)
    {
        detached = null;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            return false;
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Loose;
        detached = instance.Clone();
        return true;
    }

    public IReadOnlyList<CombatEquipmentInstance> ConfiscateAllFromCharacter(
        string characterId)
    {
        return loadoutRuntime.ConfiscateAll(characterId);
    }

    public bool TryMaterializeRecoveredEquipment(
        string instanceId,
        Vector2Int position,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
        {
            failureReason = "recovered equipment instance is missing";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            || instance.worldState != CombatEquipmentWorldState.Loose)
        {
            failureReason = "recovered equipment is still owned or unavailable";
            return false;
        }
        return TryDropExistingEquipmentToWorld(
            instance.instanceId,
            position,
            out _,
            out failureReason);
    }

    public bool TryDropExistingEquipmentToWorld(
        string instanceId,
        Vector2Int position,
        out string stackId,
        out string failureReason)
    {
        stackId = string.Empty;
        failureReason = string.Empty;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
        {
            failureReason = "equipment instance is missing";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            WorldItemStackSnapshot existing = itemStackRuntime.GetAllStacks()
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.StackId,
                        instance.sourceStackId,
                        StringComparison.Ordinal));
            if (existing == null
                || !string.Equals(
                    existing.ItemInstanceId,
                    instance.instanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    existing.ItemId,
                    PhysicalItemIds.ForEquipment(instance.definitionId),
                    StringComparison.Ordinal))
            {
                failureReason = "equipment source stack is missing or mismatched";
                return false;
            }

            stackId = existing.StackId;
            if (TryLinkToWorldStack(
                instance.instanceId,
                stackId,
                CombatEquipmentWorldState.Loose))
            {
                return true;
            }

            stackId = string.Empty;
            failureReason = "equipment source stack link failed";
            return false;
        }

        if (!itemStackRuntime.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipment(instance.definitionId),
                new ItemInstanceId(instance.instanceId),
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out stackId))
        {
            failureReason = "equipment physical stack spawn failed";
            return false;
        }
        if (TryLinkToWorldStack(
                instance.instanceId,
                stackId,
                CombatEquipmentWorldState.Loose))
        {
            return true;
        }

        string rollbackStackId = stackId;
        stackId = string.Empty;
        if (!itemStackRuntime.DeleteStack(rollbackStackId))
        {
            throw new InvalidOperationException(
                $"Equipment world-drop rollback failed for stack '{rollbackStackId}'.");
        }

        failureReason = "equipment stack link failed";
        return false;
    }

    public void HandleCharacterDeath(string characterId)
    {
        loadoutRuntime.HandleCharacterDeath(characterId);
    }

    public bool TryRestoreDurability(string instanceId, float durabilityRatio)
    {
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            return false;
        }

        instance.durabilityRatio = Mathf.Clamp01(
            Mathf.Max(instance.durabilityRatio, durabilityRatio));
        PersistPhysicalState(instance);
        return true;
    }

    public EquipmentModuleInstance CreateExpeditionModule(
        string definitionId,
        int grade,
        Vector2Int deliveryPosition,
        WorldItemStackState worldState = WorldItemStackState.Loose,
        string destinationId = "",
        bool identified = false)
    {
        return moduleRuntime.CreateExpeditionModule(
            definitionId,
            grade,
            deliveryPosition,
            worldState,
            destinationId,
            identified);
    }

    public bool TryAppraiseModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryAppraise(moduleInstanceId, facility, out failure);
    }

    public bool TryRestoreModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryRestore(moduleInstanceId, facility, out failure);
    }

    public bool TryTuneModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryTune(moduleInstanceId, facility, out failure);
    }

    public bool TryInstallModule(
        string equipmentInstanceId,
        string moduleInstanceId,
        int slotIndex,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryInstall(
            equipmentInstanceId,
            moduleInstanceId,
            slotIndex,
            facility,
            out failure);
    }

    public bool TryRemoveModule(
        string equipmentInstanceId,
        int slotIndex,
        BuildableObject facility,
        out EquipmentModuleInstance removed,
        out DomainFailure failure)
    {
        return moduleRuntime.TryRemove(
            equipmentInstanceId,
            slotIndex,
            facility,
            out removed,
            out failure);
    }

    public bool TryQueueHistoryTransfer(
        string sourceEquipmentInstanceId,
        string targetEquipmentInstanceId,
        string lineageSealStackId,
        BuildableObject facility,
        out EquipmentHistoryTransferOrder order,
        out DomainFailure failure)
    {
        return historyRuntime.TryQueue(
            sourceEquipmentInstanceId,
            targetEquipmentInstanceId,
            lineageSealStackId,
            facility,
            out order,
            out failure);
    }

    public bool ApplyHistoryTransferWork(
        string orderId,
        float work,
        BuildableObject facility,
        out bool completed,
        out DomainFailure failure)
    {
        return historyRuntime.ApplyWork(
            orderId,
            work,
            facility,
            out completed,
            out failure);
    }

    public bool TryClaimRegionLineageSeal(string regionId)
    {
        return historyRuntime.TryClaimRegionSeal(regionId);
    }

    public float GetCarriedWeight(string characterId)
    {
        return loadoutRuntime.GetCarriedWeight(characterId);
    }

    public float GetEquippedWeight(string characterId) =>
        loadoutRuntime.GetCarriedWeight(characterId);

    public DungeonCombatEquipmentSaveData Capture()
    {
        crafting.ValidateInputDestinationsBeforeCapture();
        return new DungeonCombatEquipmentSaveData
        {
            nextCraftSequence = stateStore.Current.NextCraftSequence,
            loadouts = loadoutRuntime.Capture().ToList(),
            craftOrders = crafting.CaptureOrders().ToList(),
            craftMaterialPolicies = crafting.CapturePolicies().ToList(),
            craftTerminalEffects = stateStore.Current.CraftTerminalEffects
                .Values
                .OrderBy(value => value.sourceId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList(),
            historyTransferOrders = historyRuntime.CaptureOrders().ToList(),
            claimedLineageSealRegionIds =
                historyRuntime.CaptureClaimedRegionIds().ToList()
        };
    }

    public CombatEquipmentRestoreCandidate BuildRestoreCandidate(
        DungeonCombatEquipmentSaveData saveData)
    {
        return CombatEquipmentRestoreBuilder.Build(
            saveData,
            catalog,
            crafting);
    }

    public void PublishRestoreCandidate(
        CombatEquipmentRestoreCandidate candidate)
    {
        CombatEquipmentRuntimeState restored =
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State;
        if (!crafting.TryReplaceInputDestinations(
                restored.CraftOrders,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Combat craft input restore publication failed: "
                + failureReason);
        }
        stateStore.Replace(restored);
    }

}
