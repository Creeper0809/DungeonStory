using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class EquipmentEvolutionSaveData
{
    public List<EvolutionReforgeOrder> reforgeOrders =
        new List<EvolutionReforgeOrder>();
    public List<EquipmentReattunementOrder> reattunementOrders =
        new List<EquipmentReattunementOrder>();
}

public interface IEquipmentEvolutionRuntime
{
    IReadOnlyList<EvolutionReforgeOrder> ReforgeOrders { get; }
    IReadOnlyList<EquipmentReattunementOrder> ReattunementOrders { get; }
    EquipmentEvolutionState GetState(string equipmentInstanceId);
    EquipmentEvolutionState RecordUsage(
        string equipmentInstanceId,
        string eventId,
        float mastery,
        float amount,
        string ownerPersistentId,
        int attunementPoints,
        IEnumerable<string> sourceTags = null);
    bool TryRecordUsage(
        string equipmentInstanceId,
        string eventId,
        float mastery,
        float amount,
        string ownerPersistentId,
        int attunementPoints,
        IEnumerable<string> sourceTags = null);
    EquipmentReforgePreview GetPreview(string equipmentInstanceId);
    bool TryGetActiveReforge(
        BuildableObject craftingFacility,
        out EvolutionReforgeOrder order);
    bool TryGetActiveReattunement(
        BuildableObject craftingFacility,
        out EquipmentReattunementOrder order);
    bool TryQueueReforge(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string catalystItemId,
        string stabilizerItemId,
        out EvolutionReforgeOrder order,
        out string failureReason);
    bool ApplyReforgeWork(
        string orderId,
        float workUnits,
        out EvolutionNode completedNode,
        out string failureReason);
    bool ApplyReattunementWork(
        string orderId,
        float workUnits,
        out bool completed,
        out string failureReason);
    bool CancelReforge(string orderId, out string failureReason);
    EquipmentEvolutionSaveData Capture();
    void Restore(EquipmentEvolutionSaveData saveData);
}

public interface IAttunementRuntime
{
    IReadOnlyList<EquipmentReattunementOrder> ReattunementOrders { get; }
    int GetAffinityScore(string equipmentInstanceId, string ownerPersistentId);
    bool TryGetActiveReattunement(
        BuildableObject craftingFacility,
        out EquipmentReattunementOrder order);
    bool TryQueueReattunement(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string nodeId,
        bool active,
        string catalystItemId,
        out EquipmentReattunementOrder order,
        out string failureReason);
    bool ApplyReattunementWork(
        string orderId,
        float workUnits,
        out bool completed,
        out string failureReason);
    bool CancelReattunement(
        string orderId,
        out string failureReason);
}

public sealed class EquipmentEvolutionRuntime :
    IEquipmentEvolutionRuntime,
    IAttunementRuntime
{
    private const string DefaultBindingItemId = "resource:dark-resin";

    private readonly ICombatEquipmentRuntime equipment;
    private readonly IUsageLedgerCompactor ledgerCompactor;
    private readonly IEvolutionModuleRegistry modules;
    private readonly IResourceEconomyContentCatalog economyCatalog;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IFacilityEvolutionStateComponentFactory facilityStates;
    private readonly List<EvolutionReforgeOrder> orders =
        new List<EvolutionReforgeOrder>();
    private readonly List<EquipmentReattunementOrder> reattunementOrders =
        new List<EquipmentReattunementOrder>();
    private IReadOnlyList<EvolutionReforgeOrder> ordersView;
    private IReadOnlyList<EquipmentReattunementOrder> reattunementOrdersView;

    public EquipmentEvolutionRuntime(
        ICombatEquipmentRuntime equipment,
        IUsageLedgerCompactor ledgerCompactor,
        IEvolutionModuleRegistry modules,
        IResourceEconomyContentCatalog economyCatalog,
        IWorldItemStackRuntime worldItems,
        IFacilityEvolutionStateComponentFactory facilityStates)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.ledgerCompactor = ledgerCompactor
            ?? throw new ArgumentNullException(nameof(ledgerCompactor));
        this.modules = modules
            ?? throw new ArgumentNullException(nameof(modules));
        this.economyCatalog = economyCatalog;
        this.worldItems = worldItems;
        this.facilityStates = facilityStates
            ?? throw new ArgumentNullException(nameof(facilityStates));
    }

    public IReadOnlyList<EvolutionReforgeOrder> ReforgeOrders =>
        ordersView ??= orders.AsReadOnly();
    public IReadOnlyList<EquipmentReattunementOrder> ReattunementOrders =>
        reattunementOrdersView ??= reattunementOrders.AsReadOnly();

    public EquipmentEvolutionState GetState(string equipmentInstanceId)
    {
        return RequireInstance(equipmentInstanceId).evolution?.Clone()
            ?? new EquipmentEvolutionState();
    }

    public EquipmentEvolutionState RecordUsage(
        string equipmentInstanceId,
        string eventId,
        float mastery,
        float amount,
        string ownerPersistentId,
        int attunementPoints,
        IEnumerable<string> sourceTags = null)
    {
        CombatEquipmentInstance instance = RequireInstance(equipmentInstanceId);
        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        ledgerCompactor.Record(
            state.usageLedger,
            eventId,
            amount,
            ownerPersistentId,
            instance.instanceId,
            sourceTags);
        state.mastery = Mathf.Max(0f, state.mastery + Mathf.Max(0f, mastery));
        if (!string.IsNullOrWhiteSpace(ownerPersistentId)
            && attunementPoints > 0)
        {
            AddAttunement(
                state,
                instance.instanceId,
                ownerPersistentId.Trim(),
                attunementPoints);
        }

        if (!state.reforgeReady && state.mastery + 0.001f >= state.RequiredMastery)
        {
            CompactedHistorySegment segment = ledgerCompactor.CloseGeneration(
                state.usageLedger,
                state.generation);
            state.pendingHistoryHash = segment.historyHash;
            state.pendingDirection = InferDirection(segment);
            state.reforgeReady = true;
        }

        equipment.TryUpdateEvolutionState(instance.instanceId, state);
        return state.Clone();
    }

    public bool TryRecordUsage(
        string equipmentInstanceId,
        string eventId,
        float mastery,
        float amount,
        string ownerPersistentId,
        int attunementPoints,
        IEnumerable<string> sourceTags = null)
    {
        if (!equipment.TryGetInstance(equipmentInstanceId, out _))
        {
            return false;
        }

        RecordUsage(
            equipmentInstanceId,
            eventId,
            mastery,
            amount,
            ownerPersistentId,
            attunementPoints,
            sourceTags);
        return true;
    }

    public EquipmentReforgePreview GetPreview(string equipmentInstanceId)
    {
        EquipmentEvolutionState state = GetState(equipmentInstanceId);
        EquipmentEvolutionDirection direction = state.reforgeReady
            ? state.pendingDirection
            : InferDirectionFromOpenLedger(state.usageLedger);
        int potency = EquipmentEvolutionProgression.GetMinimumCatalystPotency(
            state.generation);
        float generationScale = 1f + Mathf.Min(0.2f, state.generation * 0.01f);
        return new EquipmentReforgePreview(
            direction,
            1.04f * generationScale,
            1.12f * generationScale,
            new[] { "combat.weight", "combat.reload", "combat.accident" },
            potency);
    }

    public bool TryGetActiveReforge(
        BuildableObject craftingFacility,
        out EvolutionReforgeOrder order)
    {
        order = null;
        if (craftingFacility == null)
        {
            return false;
        }

        FacilityEvolutionStateComponent facilityState =
            facilityStates.GetOrAdd(craftingFacility);
        facilityState.InitializeIfNeeded(craftingFacility);
        string facilityId = facilityState.FacilityPersistentId;
        EvolutionReforgeOrder match = orders.FirstOrDefault(entry =>
            entry != null
            && entry.state is not EvolutionReforgeOrderState.Completed
                and not EvolutionReforgeOrderState.Cancelled
            && string.Equals(
                entry.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal));
        order = match?.Clone();
        return order != null;
    }

    public bool TryQueueReforge(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string catalystItemId,
        string stabilizerItemId,
        out EvolutionReforgeOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        if (craftingFacility == null
            || craftingFacility.isDestroy
            || craftingFacility.BuildingData?
                .GetAbility<BuildingEquipmentCraftingAbility>() == null)
        {
            failureReason = "재단조가 가능한 대장작업대가 필요합니다.";
            return false;
        }

        if (!equipment.TryGetInstance(
                equipmentInstanceId,
                out CombatEquipmentInstance instance)
            || !equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "재단조할 장비를 찾을 수 없습니다.";
            return false;
        }

        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (!state.reforgeReady)
        {
            failureReason =
                $"장비 기록이 부족합니다. {state.mastery:0.#}/{state.RequiredMastery:0.#}";
            return false;
        }

        if (HasActiveEquipmentOrder(instance.instanceId))
        {
            failureReason = "이 장비에는 이미 진행 중인 진화 작업이 있습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            failureReason = "장비를 창고나 바닥에 내려놓은 뒤 재단조할 수 있습니다.";
            return false;
        }

        if (!EvolutionCatalystItemId.TryParseCatalyst(
                catalystItemId,
                out EquipmentCatalystDefinition catalyst))
        {
            failureReason = "올바른 재단조 촉매가 아닙니다.";
            return false;
        }

        int requiredPotency =
            EquipmentEvolutionProgression.GetMinimumCatalystPotency(
                state.generation);
        if (catalyst.potency < requiredPotency)
        {
            failureReason =
                $"촉매 효능이 부족합니다. {catalyst.potency}/{requiredPotency}";
            return false;
        }

        string materialItemId = ResolvePrimaryMaterialItemId(instance);
        if (string.IsNullOrWhiteSpace(materialItemId))
        {
            failureReason = "장비의 주재료를 찾을 수 없습니다.";
            return false;
        }

        FacilityEvolutionStateComponent facilityState =
            facilityStates.GetOrAdd(craftingFacility);
        facilityState.InitializeIfNeeded(craftingFacility);
        string orderId = $"reforge:{Guid.NewGuid():N}";
        string destinationId = $"facility-reforge:{orderId}";
        Vector2Int position = craftingFacility.centerPos;
        EvolutionReforgeOrder created = new EvolutionReforgeOrder
        {
            orderId = orderId,
            equipmentInstanceId = instance.instanceId,
            facilityPersistentId = facilityState.FacilityPersistentId,
            targetGeneration = state.generation + 1,
            direction = state.pendingDirection,
            catalystItemId = catalyst.itemId,
            catalystFamily = catalyst.family,
            catalystPotency = catalyst.potency,
            catalystSourceTags = new List<string>(catalyst.sourceTags),
            primaryMaterialItemId = materialItemId,
            primaryMaterialAmount = 1,
            bindingItemId = DefaultBindingItemId,
            bindingAmount = 1,
            stabilizerItemId = stabilizerItemId?.Trim() ?? string.Empty,
            stabilizerAmount = string.IsNullOrWhiteSpace(stabilizerItemId) ? 0 : 1,
            requiredWork = EquipmentEvolutionProgression.GetReforgeWork(
                definition.RequiredCraftWork,
                state.generation),
            completedWork = 0f,
            state = EvolutionReforgeOrderState.WaitingForMaterials,
            destinationId = destinationId,
            destinationX = position.x,
            destinationY = position.y,
            lockedHistoryHash = state.pendingHistoryHash,
            lockedDirection = state.pendingDirection
        };

        Dictionary<string, int> requirements = BuildRequirements(created);
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            string requestFailure = string.Empty;
            if (worldItems == null
                || !worldItems.TryRequestItemDelivery(
                    requirement.Key,
                    requirement.Value,
                    position,
                    destinationId,
                    out int requested,
                    out requestFailure)
                || requested < requirement.Value)
            {
                worldItems?.ReleaseStacksByDestination(destinationId, position);
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? $"재료가 부족합니다: {requirement.Key}"
                    : requestFailure;
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            if (!worldItems.TryRequestStackDelivery(
                    instance.sourceStackId,
                    1,
                    position,
                    destinationId,
                    out int requestedEquipment,
                    out string equipmentFailure)
                || requestedEquipment < 1)
            {
                worldItems.ReleaseStacksByDestination(destinationId, position);
                failureReason = string.IsNullOrWhiteSpace(equipmentFailure)
                    ? "장비를 대장작업대로 운반할 수 없습니다."
                    : equipmentFailure;
                return false;
            }
        }
        else if (instance.worldState is CombatEquipmentWorldState.Equipped
                 or CombatEquipmentWorldState.Carried
                 or CombatEquipmentWorldState.ExpeditionPacked)
        {
            worldItems.ReleaseStacksByDestination(destinationId, position);
            failureReason = "장착하거나 운반 중인 장비는 먼저 창고에 내려놓아야 합니다.";
            return false;
        }

        orders.Add(created);
        order = created.Clone();
        return true;
    }

    public bool ApplyReforgeWork(
        string orderId,
        float workUnits,
        out EvolutionNode completedNode,
        out string failureReason)
    {
        completedNode = null;
        failureReason = string.Empty;
        EvolutionReforgeOrder order = orders.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.orderId,
                orderId?.Trim(),
                StringComparison.Ordinal));
        if (order == null
            || order.state is EvolutionReforgeOrderState.Completed
                or EvolutionReforgeOrderState.Cancelled)
        {
            failureReason = "진행 가능한 재단조 주문을 찾을 수 없습니다.";
            return false;
        }

        if (!EnsureMaterialsReady(order, out failureReason))
        {
            order.state = EvolutionReforgeOrderState.WaitingForMaterials;
            return false;
        }

        order.state = EvolutionReforgeOrderState.InProgress;
        order.completedWork = Mathf.Clamp(
            order.completedWork + Mathf.Max(0f, workUnits),
            0f,
            order.requiredWork);
        if (order.completedWork + 0.001f < order.requiredWork)
        {
            return true;
        }

        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance))
        {
            order.state = EvolutionReforgeOrderState.Blocked;
            failureReason = "재단조 중인 장비가 사라졌습니다.";
            return false;
        }

        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (!string.Equals(
                state.pendingHistoryHash,
                order.lockedHistoryHash,
                StringComparison.Ordinal)
            || state.pendingDirection != order.lockedDirection)
        {
            order.state = EvolutionReforgeOrderState.Blocked;
            failureReason = "고정된 장비 기록과 현재 상태가 일치하지 않습니다.";
            return false;
        }

        EvolutionNode node = BuildCompletedNode(instance, state, order);
        state.evolutionNodes.Add(node);
        state.generation = order.targetGeneration;
        state.mastery = Mathf.Max(
            0f,
            state.mastery
                - EquipmentEvolutionProgression.GetRequiredMastery(
                    state.generation - 1));
        state.reforgeReady = false;
        state.pendingHistoryHash = string.Empty;
        state.pendingDirection = EquipmentEvolutionDirection.Balanced;
        equipment.TryUpdateEvolutionState(instance.instanceId, state);
        order.state = EvolutionReforgeOrderState.Completed;
        order.completedWork = order.requiredWork;
        worldItems?.ReleaseStacksByDestination(
            order.destinationId,
            new Vector2Int(order.destinationX, order.destinationY));
        completedNode = node.Clone();
        return true;
    }

    public bool CancelReforge(
        string orderId,
        out string failureReason)
    {
        failureReason = string.Empty;
        EvolutionReforgeOrder order = orders.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.orderId,
                orderId?.Trim(),
                StringComparison.Ordinal));
        if (order == null
            || order.state == EvolutionReforgeOrderState.Completed)
        {
            failureReason = "취소할 재단조 주문을 찾을 수 없습니다.";
            return false;
        }

        order.state = EvolutionReforgeOrderState.Cancelled;
        worldItems?.ReleaseStacksByDestination(
            order.destinationId,
            new Vector2Int(order.destinationX, order.destinationY));
        return true;
    }

    public int GetAffinityScore(
        string equipmentInstanceId,
        string ownerPersistentId)
    {
        EquipmentEvolutionState state = GetState(equipmentInstanceId);
        return state.attunements.FirstOrDefault(record =>
                record != null
                && string.Equals(
                    record.ownerPersistentId,
                    ownerPersistentId?.Trim(),
                    StringComparison.Ordinal))
            ?.affinityScore ?? 0;
    }

    public bool TryGetActiveReattunement(
        BuildableObject craftingFacility,
        out EquipmentReattunementOrder order)
    {
        order = null;
        if (craftingFacility == null)
        {
            return false;
        }

        FacilityEvolutionStateComponent facilityState =
            facilityStates.GetOrAdd(craftingFacility);
        facilityState.InitializeIfNeeded(craftingFacility);
        string facilityId = facilityState.FacilityPersistentId;
        EquipmentReattunementOrder match = reattunementOrders.FirstOrDefault(
            entry => entry != null
                && entry.state is not EvolutionReforgeOrderState.Completed
                    and not EvolutionReforgeOrderState.Cancelled
                && string.Equals(
                    entry.facilityPersistentId,
                    facilityId,
                    StringComparison.Ordinal));
        order = match?.Clone();
        return order != null;
    }

    public bool TryQueueReattunement(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string nodeId,
        bool active,
        string catalystItemId,
        out EquipmentReattunementOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        if (craftingFacility == null
            || craftingFacility.isDestroy
            || craftingFacility.BuildingData?
                .GetAbility<BuildingEquipmentCraftingAbility>() == null)
        {
            failureReason = "재귀속 작업이 가능한 대장작업대가 필요합니다.";
            return false;
        }

        if (!equipment.TryGetInstance(
                equipmentInstanceId,
                out CombatEquipmentInstance instance)
            || !equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "재귀속할 장비를 찾을 수 없습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            failureReason = "장비를 창고나 바닥에 내려놓은 뒤 재귀속할 수 있습니다.";
            return false;
        }

        if (HasActiveEquipmentOrder(instance.instanceId))
        {
            failureReason = "이 장비에는 이미 진행 중인 진화 작업이 있습니다.";
            return false;
        }

        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (!TryBuildHistoricalActivationResult(
                state,
                nodeId,
                active,
                out List<string> resultingIds,
                out failureReason))
        {
            return false;
        }

        if (state.activeHistoricalNodeIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(resultingIds, StringComparer.Ordinal))
        {
            failureReason = "선택한 귀속 상태가 이미 적용되어 있습니다.";
            return false;
        }

        if (!EvolutionCatalystItemId.TryParseCatalyst(
                catalystItemId,
                out EquipmentCatalystDefinition catalyst))
        {
            failureReason = "올바른 재귀속 촉매가 아닙니다.";
            return false;
        }

        int requiredPotency =
            EquipmentEvolutionProgression.GetMinimumCatalystPotency(
                state.generation);
        if (catalyst.potency < requiredPotency)
        {
            failureReason =
                $"촉매 효능이 부족합니다. {catalyst.potency}/{requiredPotency}";
            return false;
        }

        FacilityEvolutionStateComponent facilityState =
            facilityStates.GetOrAdd(craftingFacility);
        facilityState.InitializeIfNeeded(craftingFacility);
        string orderId = $"reattune:{Guid.NewGuid():N}";
        string destinationId = $"facility-reattune:{orderId}";
        Vector2Int position = craftingFacility.centerPos;
        EquipmentReattunementOrder created =
            new EquipmentReattunementOrder
            {
                orderId = orderId,
                equipmentInstanceId = instance.instanceId,
                facilityPersistentId = facilityState.FacilityPersistentId,
                targetNodeId = nodeId?.Trim() ?? string.Empty,
                targetActive = active,
                resultingActiveNodeIds = resultingIds,
                catalystItemId = catalyst.itemId,
                catalystPotency = catalyst.potency,
                requiredWork =
                    EquipmentEvolutionProgression.GetReattunementWork(
                        definition.RequiredCraftWork,
                        state.generation),
                state = EvolutionReforgeOrderState.WaitingForMaterials,
                destinationId = destinationId,
                destinationX = position.x,
                destinationY = position.y,
                lockedStateHash = ComputeAttunementStateHash(state)
            };

        string catalystFailure = string.Empty;
        int requestedCatalyst = 0;
        if (worldItems == null
            || !worldItems.TryRequestItemDelivery(
                created.catalystItemId,
                1,
                position,
                destinationId,
                out requestedCatalyst,
                out catalystFailure)
            || requestedCatalyst < 1)
        {
            worldItems?.ReleaseStacksByDestination(destinationId, position);
            failureReason = string.IsNullOrWhiteSpace(catalystFailure)
                ? "재귀속 촉매가 부족합니다."
                : catalystFailure;
            return false;
        }

        if (!worldItems.TryRequestStackDelivery(
                instance.sourceStackId,
                1,
                position,
                destinationId,
                out int requestedEquipment,
                out string equipmentFailure)
            || requestedEquipment < 1)
        {
            worldItems.ReleaseStacksByDestination(destinationId, position);
            failureReason = string.IsNullOrWhiteSpace(equipmentFailure)
                ? "장비를 대장작업대로 운반할 수 없습니다."
                : equipmentFailure;
            return false;
        }

        reattunementOrders.Add(created);
        order = created.Clone();
        return true;
    }

    public bool ApplyReattunementWork(
        string orderId,
        float workUnits,
        out bool completed,
        out string failureReason)
    {
        completed = false;
        failureReason = string.Empty;
        EquipmentReattunementOrder order = reattunementOrders.FirstOrDefault(
            entry => entry != null
                && string.Equals(
                    entry.orderId,
                    orderId?.Trim(),
                    StringComparison.Ordinal));
        if (order == null
            || order.state is EvolutionReforgeOrderState.Completed
                or EvolutionReforgeOrderState.Cancelled)
        {
            failureReason = "진행 가능한 재귀속 주문을 찾을 수 없습니다.";
            return false;
        }

        if (!EnsureReattunementMaterialsReady(order, out failureReason))
        {
            order.state = EvolutionReforgeOrderState.WaitingForMaterials;
            return false;
        }

        order.state = EvolutionReforgeOrderState.InProgress;
        order.completedWork = Mathf.Clamp(
            order.completedWork + Mathf.Max(0f, workUnits),
            0f,
            order.requiredWork);
        if (order.completedWork + 0.001f < order.requiredWork)
        {
            return true;
        }

        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance))
        {
            order.state = EvolutionReforgeOrderState.Blocked;
            failureReason = "재귀속 중인 장비가 사라졌습니다.";
            return false;
        }

        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (!string.Equals(
                ComputeAttunementStateHash(state),
                order.lockedStateHash,
                StringComparison.Ordinal))
        {
            order.state = EvolutionReforgeOrderState.Blocked;
            failureReason = "고정한 귀속 상태와 현재 장비 상태가 일치하지 않습니다.";
            return false;
        }

        if (!TryBuildHistoricalActivationResult(
                state,
                order.targetNodeId,
                order.targetActive,
                out List<string> currentResult,
                out failureReason)
            || !currentResult.SequenceEqual(
                order.resultingActiveNodeIds,
                StringComparer.Ordinal))
        {
            order.state = EvolutionReforgeOrderState.Blocked;
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason = "재귀속 결과가 주문 시점과 달라졌습니다.";
            }
            return false;
        }

        state.activeHistoricalNodeIds =
            new List<string>(order.resultingActiveNodeIds);
        equipment.TryUpdateEvolutionState(instance.instanceId, state);
        order.state = EvolutionReforgeOrderState.Completed;
        order.completedWork = order.requiredWork;
        worldItems?.ReleaseStacksByDestination(
            order.destinationId,
            new Vector2Int(order.destinationX, order.destinationY));
        completed = true;
        return true;
    }

    public bool CancelReattunement(
        string orderId,
        out string failureReason)
    {
        failureReason = string.Empty;
        EquipmentReattunementOrder order = reattunementOrders.FirstOrDefault(
            entry => entry != null
                && string.Equals(
                    entry.orderId,
                    orderId?.Trim(),
                    StringComparison.Ordinal));
        if (order == null
            || order.state == EvolutionReforgeOrderState.Completed)
        {
            failureReason = "취소할 재귀속 주문을 찾을 수 없습니다.";
            return false;
        }

        order.state = EvolutionReforgeOrderState.Cancelled;
        worldItems?.ReleaseStacksByDestination(
            order.destinationId,
            new Vector2Int(order.destinationX, order.destinationY));
        return true;
    }

    public EquipmentEvolutionSaveData Capture()
    {
        return new EquipmentEvolutionSaveData
        {
            reforgeOrders = orders
                .Where(order => order != null
                    && order.state is not EvolutionReforgeOrderState.Completed
                        and not EvolutionReforgeOrderState.Cancelled)
                .Select(order => order.Clone())
                .ToList(),
            reattunementOrders = reattunementOrders
                .Where(order => order != null
                    && order.state is not EvolutionReforgeOrderState.Completed
                        and not EvolutionReforgeOrderState.Cancelled)
                .Select(order => order.Clone())
                .ToList()
        };
    }

    public void Restore(EquipmentEvolutionSaveData saveData)
    {
        orders.Clear();
        reattunementOrders.Clear();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (EvolutionReforgeOrder source in saveData?.reforgeOrders
                     ?? new List<EvolutionReforgeOrder>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || !ids.Add(source.orderId)
                || !equipment.TryGetInstance(source.equipmentInstanceId, out _)
                || source.state is EvolutionReforgeOrderState.Completed
                    or EvolutionReforgeOrderState.Cancelled)
            {
                continue;
            }

            orders.Add(source.Clone());
        }

        foreach (EquipmentReattunementOrder source in
                 saveData?.reattunementOrders
                 ?? new List<EquipmentReattunementOrder>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || !ids.Add(source.orderId)
                || !equipment.TryGetInstance(source.equipmentInstanceId, out _)
                || source.state is EvolutionReforgeOrderState.Completed
                    or EvolutionReforgeOrderState.Cancelled)
            {
                continue;
            }

            reattunementOrders.Add(source.Clone());
        }
    }

    private bool EnsureMaterialsReady(
        EvolutionReforgeOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order.materialsConsumed)
        {
            return true;
        }

        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance))
        {
            failureReason = "재단조 중인 장비가 사라졌습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            WorldItemStackSnapshot equipmentStack = worldItems?
                .GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        instance.sourceStackId,
                        StringComparison.Ordinal));
            if (equipmentStack == null
                || equipmentStack.State != WorldItemStackState.FacilityBuffer
                || !string.Equals(
                    equipmentStack.DestinationId,
                    order.destinationId,
                    StringComparison.Ordinal))
            {
                failureReason = "재단조할 장비를 작업대로 운반하는 중입니다.";
                return false;
            }
        }

        if (worldItems == null
            || !worldItems.TryConsumeFacilityItemBuffer(
                order.destinationId,
                BuildRequirements(order),
                out failureReason))
        {
            return false;
        }

        order.materialsConsumed = true;
        order.equipmentDelivered = true;
        order.state = EvolutionReforgeOrderState.Ready;
        return true;
    }

    private bool EnsureReattunementMaterialsReady(
        EquipmentReattunementOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order.materialsConsumed)
        {
            return true;
        }

        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance))
        {
            failureReason = "재귀속 중인 장비가 사라졌습니다.";
            return false;
        }

        WorldItemStackSnapshot equipmentStack = worldItems?
            .GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    instance.sourceStackId,
                    StringComparison.Ordinal));
        if (equipmentStack == null
            || equipmentStack.State != WorldItemStackState.FacilityBuffer
            || !string.Equals(
                equipmentStack.DestinationId,
                order.destinationId,
                StringComparison.Ordinal))
        {
            failureReason = "재귀속할 장비를 작업대로 운반하는 중입니다.";
            return false;
        }

        Dictionary<string, int> requirements =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [order.catalystItemId] = 1
            };
        if (worldItems == null
            || !worldItems.TryConsumeFacilityItemBuffer(
                order.destinationId,
                requirements,
                out failureReason))
        {
            return false;
        }

        order.materialsConsumed = true;
        order.equipmentDelivered = true;
        order.state = EvolutionReforgeOrderState.Ready;
        return true;
    }

    private bool HasActiveEquipmentOrder(string equipmentInstanceId)
    {
        string normalized = equipmentInstanceId?.Trim() ?? string.Empty;
        return orders.Any(order => order != null
                && order.state is not EvolutionReforgeOrderState.Completed
                    and not EvolutionReforgeOrderState.Cancelled
                && string.Equals(
                    order.equipmentInstanceId,
                    normalized,
                    StringComparison.Ordinal))
            || reattunementOrders.Any(order => order != null
                && order.state is not EvolutionReforgeOrderState.Completed
                    and not EvolutionReforgeOrderState.Cancelled
                && string.Equals(
                    order.equipmentInstanceId,
                    normalized,
                    StringComparison.Ordinal));
    }

    private static bool TryBuildHistoricalActivationResult(
        EquipmentEvolutionState state,
        string nodeId,
        bool active,
        out List<string> resultingIds,
        out string failureReason)
    {
        resultingIds = new List<string>();
        failureReason = string.Empty;
        if (state == null)
        {
            failureReason = "장비 진화 상태를 찾을 수 없습니다.";
            return false;
        }

        string normalizedNodeId = nodeId?.Trim() ?? string.Empty;
        EvolutionNode node = state.evolutionNodes.FirstOrDefault(entry =>
            entry != null
            && entry.historical
            && entry.playerVisible
            && string.Equals(
                entry.nodeId,
                normalizedNodeId,
                StringComparison.Ordinal));
        if (node == null)
        {
            failureReason = "공개된 귀속 역사 노드를 찾을 수 없습니다.";
            return false;
        }

        HashSet<string> activeIds = new HashSet<string>(
            state.activeHistoricalNodeIds
                ?? new List<string>(),
            StringComparer.Ordinal);
        if (active)
        {
            if (!string.IsNullOrWhiteSpace(node.parentNodeId)
                && !activeIds.Contains(node.parentNodeId))
            {
                failureReason = "부모 역사 노드를 먼저 활성화해야 합니다.";
                return false;
            }

            if (!activeIds.Contains(node.nodeId)
                && activeIds.Count >= state.ResonanceBudget)
            {
                failureReason =
                    $"공명 예산이 부족합니다. {activeIds.Count}/{state.ResonanceBudget}";
                return false;
            }

            activeIds.Add(node.nodeId);
        }
        else
        {
            DisableNodeAndDescendants(
                state.evolutionNodes,
                activeIds,
                node.nodeId);
        }

        resultingIds = activeIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        return true;
    }

    private static string ComputeAttunementStateHash(
        EquipmentEvolutionState state)
    {
        IEnumerable<string> nodeState = (state?.evolutionNodes
                ?? new List<EvolutionNode>())
            .Where(node => node != null && node.historical)
            .OrderBy(node => node.nodeId, StringComparer.Ordinal)
            .Select(node => string.Join(
                ":",
                node.nodeId ?? string.Empty,
                node.parentNodeId ?? string.Empty,
                node.effectId ?? string.Empty,
                node.playerVisible ? "1" : "0"));
        IEnumerable<string> activeState = (state?.activeHistoricalNodeIds
                ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.Ordinal);
        return StableEvolutionHash.Compute(string.Join(
            "|",
            state?.generation ?? 0,
            string.Join(",", nodeState),
            string.Join(",", activeState)));
    }

    private EvolutionNode BuildCompletedNode(
        CombatEquipmentInstance instance,
        EquipmentEvolutionState state,
        EvolutionReforgeOrder order)
    {
        string moduleId = ResolveModuleId(
            order.lockedDirection,
            order.catalystFamily);
        int seed = StableEvolutionHash.ToSeed(
            instance.instanceId,
            order.targetGeneration.ToString(),
            order.lockedHistoryHash,
            order.catalystFamily,
            order.catalystPotency.ToString());
        System.Random random = new System.Random(seed);
        float potencyScale = 1f
            + Mathf.Min(0.75f, Mathf.Max(0, order.catalystPotency - 1) * 0.08f);
        potencyScale *= GetCatalystFamilyPotencyScale(
            order.catalystFamily);
        float rollScale = Mathf.Lerp(
            0.75f,
            1.5f,
            (float)random.NextDouble());
        bool stabilized = order.stabilizerAmount > 0;
        bool risky = !stabilized
            && (order.catalystFamily.IndexOf(
                    "offense",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || order.catalystFamily.IndexOf(
                    "arcane",
                    StringComparison.OrdinalIgnoreCase) >= 0);
        string parentNodeId = state.evolutionNodes
            .Where(node => node != null && !node.historical)
            .OrderByDescending(node => node.generation)
            .ThenBy(node => node.nodeId, StringComparer.Ordinal)
            .Select(node => node.nodeId)
            .FirstOrDefault() ?? string.Empty;
        string nodeHash = StableEvolutionHash.Compute(
            instance.instanceId + "|" + order.orderId);
        return new EvolutionNode
        {
            nodeId = $"equipment-node:{nodeHash}",
            parentNodeId = parentNodeId,
            effectId = moduleId,
            burdenEffectId = risky ? "equipment:risky" : string.Empty,
            generation = order.targetGeneration,
            active = true,
            historical = false,
            displayName = modules.TryGet(
                moduleId,
                out EvolutionModuleDefinition definition)
                    ? definition.DisplayName
                    : moduleId,
            description = string.Empty,
            evidenceIds = state.usageLedger.compactedSegments
                .OrderByDescending(segment => segment.lastGeneration)
                .SelectMany(segment => segment.keyEvents)
                .Where(entry => entry != null)
                .Take(8)
                .Select(entry => entry.evidenceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            activationRule = new EvolutionModuleActivationRule(),
            potencyMultiplier = potencyScale * rollScale
        };
    }

    private string ResolvePrimaryMaterialItemId(
        CombatEquipmentInstance instance)
    {
        if (economyCatalog == null
            || !economyCatalog.TryGetMaterial(
                instance.materialId,
                out CraftMaterialDefinitionSO material))
        {
            return string.Empty;
        }

        return material.ItemId;
    }

    private static Dictionary<string, int> BuildRequirements(
        EvolutionReforgeOrder order)
    {
        Dictionary<string, int> result =
            new Dictionary<string, int>(StringComparer.Ordinal);
        AddRequirement(
            result,
            order.primaryMaterialItemId,
            order.primaryMaterialAmount);
        AddRequirement(result, order.catalystItemId, 1);
        AddRequirement(result, order.bindingItemId, order.bindingAmount);
        AddRequirement(
            result,
            order.stabilizerItemId,
            order.stabilizerAmount);
        return result;
    }

    private static void AddRequirement(
        IDictionary<string, int> destination,
        string itemId,
        int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return;
        }

        string normalized = itemId.Trim();
        destination.TryGetValue(normalized, out int current);
        destination[normalized] = current + amount;
    }

    private static EquipmentEvolutionDirection InferDirection(
        CompactedHistorySegment segment)
    {
        string source = string.Join(
            "|",
            segment.metrics
                .OrderByDescending(metric => Mathf.Abs(metric.value))
                .Select(metric => metric.metricId)
                .Concat(segment.sourceTags))
            .ToLowerInvariant();
        if (ContainsAny(source, "boss", "kill", "execution"))
        {
            return EquipmentEvolutionDirection.Execution;
        }
        if (ContainsAny(source, "absorb", "block", "armor", "shield"))
        {
            return EquipmentEvolutionDirection.Protection;
        }
        if (ContainsAny(source, "downed", "survive", "recovery"))
        {
            return EquipmentEvolutionDirection.Survival;
        }
        if (ContainsAny(source, "intercept", "guard", "defense"))
        {
            return EquipmentEvolutionDirection.Interception;
        }
        if (ContainsAny(source, "long", "medium", "ranged", "shoot"))
        {
            return EquipmentEvolutionDirection.Ranged;
        }
        if (ContainsAny(source, "accuracy", "hit"))
        {
            return EquipmentEvolutionDirection.Accuracy;
        }
        if (ContainsAny(source, "melee", "contact", "near"))
        {
            return EquipmentEvolutionDirection.Melee;
        }
        return EquipmentEvolutionDirection.Balanced;
    }

    private static EquipmentEvolutionDirection InferDirectionFromOpenLedger(
        UsageLedger ledger)
    {
        CompactedHistorySegment synthetic = new CompactedHistorySegment
        {
            metrics = (ledger?.currentGenerationEvents
                    ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .GroupBy(entry => entry.eventId, StringComparer.Ordinal)
                .Select(group => new UsageLedgerMetric
                {
                    metricId = group.Key,
                    value = group.Sum(entry => entry.amount)
                })
                .ToList(),
            sourceTags = (ledger?.currentGenerationEvents
                    ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .SelectMany(entry => entry.sourceTags)
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
        return InferDirection(synthetic);
    }

    private static string ResolveModuleId(
        EquipmentEvolutionDirection direction,
        string catalystFamily)
    {
        return direction switch
        {
            EquipmentEvolutionDirection.Melee
                or EquipmentEvolutionDirection.Execution => "equipment:melee",
            EquipmentEvolutionDirection.Ranged
                or EquipmentEvolutionDirection.Accuracy => "equipment:ranged",
            EquipmentEvolutionDirection.Interception
                or EquipmentEvolutionDirection.Protection => "equipment:guard",
            EquipmentEvolutionDirection.Survival => "equipment:survivor",
            _ => catalystFamily?.IndexOf(
                    "defense",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? "equipment:guard"
                : catalystFamily?.IndexOf(
                    "survival",
                    StringComparison.OrdinalIgnoreCase) >= 0
                    ? "equipment:survivor"
                    : "equipment:melee"
        };
    }

    public static float GetCatalystFamilyPotencyScale(string catalystFamily)
    {
        string normalized =
            catalystFamily?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("arcane"))
        {
            return 1.1f;
        }

        if (normalized.Contains("offense"))
        {
            return 1.07f;
        }

        if (normalized.Contains("defense"))
        {
            return 1.04f;
        }

        if (normalized.Contains("authority"))
        {
            return 1.02f;
        }

        if (normalized.Contains("survival"))
        {
            return 0.98f;
        }

        return 1f;
    }

    private static void AddAttunement(
        EquipmentEvolutionState state,
        string equipmentInstanceId,
        string ownerPersistentId,
        int points)
    {
        AttunementRecord record = state.attunements.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.ownerPersistentId,
                ownerPersistentId,
                StringComparison.Ordinal));
        if (record == null)
        {
            record = new AttunementRecord
            {
                ownerPersistentId = ownerPersistentId,
                startedGeneration = state.generation
            };
            state.attunements.Add(record);
        }

        int previousTier = record.attainedTier;
        record.affinityScore = Mathf.Max(0, record.affinityScore + points);
        int nextTier = record.affinityScore >= 250
            ? 3
            : record.affinityScore >= 100
                ? 2
                : record.affinityScore >= 30
                    ? 1
                    : 0;
        for (int tier = previousTier + 1; tier <= nextTier; tier++)
        {
            CreateAttunementHistoryNode(
                state,
                record,
                equipmentInstanceId,
                tier);
        }

        record.attainedTier = nextTier;
    }

    private static void CreateAttunementHistoryNode(
        EquipmentEvolutionState state,
        AttunementRecord record,
        string equipmentInstanceId,
        int tier)
    {
        string historyHash = StableEvolutionHash.Compute(string.Join(
            "|",
            equipmentInstanceId,
            record.ownerPersistentId,
            tier.ToString(),
            state.usageLedger != null
                ? string.Join(
                    ",",
                    state.usageLedger.currentGenerationEvents
                        .Where(entry => entry != null)
                        .OrderBy(entry => entry.sequence)
                        .Select(entry =>
                            $"{entry.evidenceId}:{entry.eventId}:{entry.amount:R}"))
                : string.Empty));
        string parentNodeId = record.historyNodeIds.LastOrDefault()
            ?? string.Empty;
        EquipmentEvolutionDirection direction =
            InferDirectionFromOpenLedger(state.usageLedger);
        string effectId = ResolveModuleId(direction, string.Empty);
        string historyNodeHash = StableEvolutionHash.Compute(
            equipmentInstanceId
            + "|"
            + record.ownerPersistentId
            + "|"
            + tier
            + "|"
            + historyHash);
        string nodeId = $"equipment-history:{historyNodeHash}";
        EvolutionNode node = new EvolutionNode
        {
            nodeId = nodeId,
            parentNodeId = parentNodeId,
            effectId = effectId,
            burdenEffectId = string.Empty,
            generation = state.generation,
            active = true,
            historical = true,
            playerVisible = false,
            displayName = string.Empty,
            description = string.Empty,
            potencyMultiplier = tier switch
            {
                1 => 0.35f,
                2 => 0.55f,
                _ => 0.8f
            },
            activationRule = new EvolutionModuleActivationRule()
        };
        EvolutionNarrativeRequestSnapshot request =
            EvolutionNarrativeRequestFactory.Create(
                EvolutionNarrativeTargetKind.Equipment,
                equipmentInstanceId,
                node,
                historyHash,
                state.usageLedger,
                state.ResonanceBudget);
        node.evidenceIds = new List<string>(request.evidenceIds);
        state.evolutionNodes.Add(node);
        state.narrativeRequests ??=
            new List<EvolutionNarrativeRequestSnapshot>();
        state.narrativeRequests.Add(request);
        record.historyNodeIds.Add(nodeId);
        if (string.IsNullOrWhiteSpace(record.rootNodeId))
        {
            record.rootNodeId = nodeId;
        }

        state.activeHistoricalNodeIds ??= new List<string>();
        if (state.activeHistoricalNodeIds.Count < state.ResonanceBudget)
        {
            state.activeHistoricalNodeIds.Add(nodeId);
        }
    }

    private static void DisableNodeAndDescendants(
        IReadOnlyList<EvolutionNode> nodes,
        ISet<string> activeIds,
        string rootNodeId)
    {
        Queue<string> queue = new Queue<string>();
        queue.Enqueue(rootNodeId);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            activeIds.Remove(current);
            foreach (EvolutionNode child in nodes.Where(node =>
                         node != null
                         && string.Equals(
                             node.parentNodeId,
                             current,
                             StringComparison.Ordinal)))
            {
                queue.Enqueue(child.nodeId);
            }
        }
    }

    private CombatEquipmentInstance RequireInstance(string instanceId)
    {
        if (!equipment.TryGetInstance(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance))
        {
            throw new KeyNotFoundException(
                $"Unknown combat equipment instance '{instanceId}'.");
        }

        return instance;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(value =>
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}

public static class EvolutionCatalystItemId
{
    private const string CatalystPrefix = "evolution:catalyst:";
    private const string ResiduePrefix = "evolution:residue:";

    public static string BuildCatalyst(string family, int potency)
    {
        string normalized = NormalizeFamily(family);
        return $"{CatalystPrefix}{normalized}:{Mathf.Max(1, potency)}";
    }

    public static string BuildResidue(int potency)
    {
        return $"{ResiduePrefix}{Mathf.Max(1, potency)}";
    }

    public static bool TryParseCatalyst(
        string itemId,
        out EquipmentCatalystDefinition definition)
    {
        definition = null;
        string normalized = itemId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(CatalystPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string payload = normalized.Substring(CatalystPrefix.Length);
        int separator = payload.LastIndexOf(':');
        if (separator <= 0
            || !int.TryParse(
                payload.Substring(separator + 1),
                out int potency)
            || potency <= 0)
        {
            return false;
        }

        string family = NormalizeFamily(payload.Substring(0, separator));
        definition = new EquipmentCatalystDefinition
        {
            itemId = normalized,
            family = family,
            potency = potency,
            sourceTags = new List<string> { family }
        };
        return true;
    }

    public static bool TryParseResidue(string itemId, out int potency)
    {
        potency = 0;
        string normalized = itemId?.Trim() ?? string.Empty;
        return normalized.StartsWith(ResiduePrefix, StringComparison.Ordinal)
            && int.TryParse(
                normalized.Substring(ResiduePrefix.Length),
                out potency)
            && potency > 0;
    }

    private static string NormalizeFamily(string family)
    {
        string normalized = family?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.StartsWith("catalyst:", StringComparison.Ordinal))
        {
            normalized = normalized.Substring("catalyst:".Length);
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? "universal"
            : normalized.Replace(':', '-');
    }
}
