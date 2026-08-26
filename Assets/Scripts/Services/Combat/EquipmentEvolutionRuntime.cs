using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using static EquipmentEvolutionRules;

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
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IFacilityEvolutionStateComponentFactory facilityStates;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private EquipmentEvolutionAggregateState CurrentState =>
        aggregateRootStore.GetOrCreate(
            () => new EquipmentEvolutionAggregateState());
    private EquipmentEvolutionAggregateState WritableState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new EquipmentEvolutionAggregateState(),
            state => state.DeepClone());
    private List<EvolutionReforgeOrder> orders => WritableState.ReforgeOrders;
    private List<EquipmentReattunementOrder> reattunementOrders =>
        WritableState.ReattunementOrders;

    public EquipmentEvolutionRuntime(
        ICombatEquipmentRuntime equipment,
        IUsageLedgerCompactor ledgerCompactor,
        IEvolutionModuleRegistry modules,
        IResourceEconomyContentCatalog economyCatalog,
        IWorldItemStackRuntime worldItems,
        IPhysicalItemBatchDispositionService batchDispositions,
        IFacilityEvolutionStateComponentFactory facilityStates,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.ledgerCompactor = ledgerCompactor
            ?? throw new ArgumentNullException(nameof(ledgerCompactor));
        this.modules = modules
            ?? throw new ArgumentNullException(nameof(modules));
        this.economyCatalog = economyCatalog;
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        this.facilityStates = facilityStates
            ?? throw new ArgumentNullException(nameof(facilityStates));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyList<EvolutionReforgeOrder> ReforgeOrders =>
        CurrentState.ReforgeOrders.Select(order => order.Clone()).ToArray();
    public IReadOnlyList<EquipmentReattunementOrder> ReattunementOrders =>
        CurrentState.ReattunementOrders.Select(order => order.Clone()).ToArray();

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
        IEnumerable<string> sourceTags = null,
        HistoricalEvidenceKind historicalEvidenceKind = HistoricalEvidenceKind.None,
        string outcomeId = "",
        int repeatCount = 1)
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
            sourceTags,
            historicalEvidenceKind: historicalEvidenceKind,
            outcomeId: outcomeId,
            generation: state.generation,
            repeatCount: repeatCount);
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
        IEnumerable<string> sourceTags = null,
        HistoricalEvidenceKind historicalEvidenceKind = HistoricalEvidenceKind.None,
        string outcomeId = "",
        int repeatCount = 1)
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
            sourceTags,
            historicalEvidenceKind,
            outcomeId,
            repeatCount);
        return true;
    }

    public EquipmentReforgePreview GetPreview(string equipmentInstanceId)
    {
        EquipmentEvolutionState state = GetState(equipmentInstanceId);
        EquipmentEvolutionDirection direction = state.reforgeReady
            ? state.pendingDirection
            : InferDirectionFromOpenLedger(state.usageLedger);
        int requiredProgressionLevel = EquipmentEvolutionProgression
            .GetMinimumCatalystProgressionLevel(state.generation);
        float generationScale = 1f + Mathf.Min(0.2f, state.generation * 0.01f);
        return new EquipmentReforgePreview(
            direction,
            1.04f * generationScale,
            1.12f * generationScale,
            new[] { "combat.weight", "combat.reload", "combat.accident" },
            requiredProgressionLevel);
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
        if (instance.worldState == CombatEquipmentWorldState.RetailStock)
        {
            failureReason = "상점 재고인 장비는 재단조할 수 없습니다.";
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

        int requiredProgressionLevel =
            EquipmentEvolutionProgression.GetMinimumCatalystProgressionLevel(
                state.generation);
        if (catalyst.progressionLevel < requiredProgressionLevel)
        {
            failureReason =
                $"촉매 진행 단계가 부족합니다. {catalyst.progressionLevel}/{requiredProgressionLevel}";
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

        if (string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            worldItems.ReleaseStacksByDestination(destinationId, position);
            failureReason =
                "장비를 물리 창고나 바닥에 내려놓은 뒤 재단조할 수 있습니다.";
            return false;
        }
        else
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
            if (!order.materialsConsumed)
            {
                order.state = EvolutionReforgeOrderState.WaitingForMaterials;
            }
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

    public bool TryConfigurePrecision(
        string orderId,
        ReforgePrecisionSelection selection,
        int goldCost,
        out string failureReason)
    {
        EvolutionReforgeOrder order = orders.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.orderId,
                orderId?.Trim(),
                StringComparison.Ordinal));
        if (order == null
            || order.state is EvolutionReforgeOrderState.Completed
                or EvolutionReforgeOrderState.Cancelled
                or EvolutionReforgeOrderState.InProgress)
        {
            failureReason = "정밀 설정을 적용할 재단조 주문이 없습니다.";
            return false;
        }

        selection ??= new ReforgePrecisionSelection();
        if (selection.SelectedCount > 2)
        {
            failureReason = "유료 정밀 서비스는 최대 두 개까지 선택할 수 있습니다.";
            return false;
        }

        order.preciseCalibration = selection.preciseCalibration;
        order.burdenSuppression = selection.burdenSuppression;
        order.externalTechnicalSupport =
            selection.externalTechnicalSupport;
        order.suppressedBurdenEffectId =
            selection.suppressedBurdenEffectId?.Trim() ?? string.Empty;
        order.precisionGoldCost = Mathf.Max(0, goldCost);
        order.resultVariance = selection.preciseCalibration ? 0.04f : 0.12f;
        if (selection.externalTechnicalSupport)
        {
            order.requiredWork = Mathf.Max(0.1f, order.requiredWork * 0.75f);
        }

        failureReason = string.Empty;
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

        if (order.materialsConsumed
            || !string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            failureReason =
                "재료가 재단조 재공품으로 이전된 주문은 취소할 수 없습니다.";
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
        if (instance.worldState == CombatEquipmentWorldState.RetailStock)
        {
            failureReason = "상점 재고인 장비는 재귀속할 수 없습니다.";
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

        int requiredProgressionLevel =
            EquipmentEvolutionProgression.GetMinimumCatalystProgressionLevel(
                state.generation);
        if (catalyst.progressionLevel < requiredProgressionLevel)
        {
            failureReason =
                $"촉매 진행 단계가 부족합니다. {catalyst.progressionLevel}/{requiredProgressionLevel}";
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
            if (!order.materialsConsumed)
            {
                order.state = EvolutionReforgeOrderState.WaitingForMaterials;
            }
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


        if (order.materialsConsumed
            || !string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            failureReason =
                "촉매가 재귀속 재공품으로 이전된 주문은 취소할 수 없습니다.";
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
            reforgeOrders = CurrentState.ReforgeOrders
                .Where(order => order != null
                    && order.state is not EvolutionReforgeOrderState.Completed
                        and not EvolutionReforgeOrderState.Cancelled)
                .Select(order => order.Clone())
                .ToList(),
            reattunementOrders = CurrentState.ReattunementOrders
                .Where(order => order != null
                    && order.state is not EvolutionReforgeOrderState.Completed
                        and not EvolutionReforgeOrderState.Cancelled)
                .Select(order => order.Clone())
                .ToList()
        };
    }

    public EquipmentEvolutionRestoreCandidate BuildRestoreCandidate(
        EquipmentEvolutionSaveData saveData)
    {
        return EquipmentEvolutionRestoreBuilder.Build(saveData);
    }

    public void PublishRestoreCandidate(
        EquipmentEvolutionRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }

    private bool EnsureMaterialsReady(
        EvolutionReforgeOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order.materialsConsumed
            && string.IsNullOrEmpty(order.materialTransferOperationId))
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

        if (!EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                order,
                worldItems,
                batchDispositions,
                instance.sourceStackId,
                out failureReason))
        {
            return false;
        }

        return true;
    }

    private bool EnsureReattunementMaterialsReady(
        EquipmentReattunementOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order.materialsConsumed
            && string.IsNullOrEmpty(order.materialTransferOperationId))
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

        if (!EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                order,
                worldItems,
                batchDispositions,
                instance.sourceStackId,
                out failureReason))
        {
            return false;
        }

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
            && entry.mechanicallyUnlocked
            && entry.uiVisible
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
        IRandomStream random = new DeterministicRandomSequence(seed);
        float potencyScale = 1f
            + Mathf.Min(0.75f, Mathf.Max(0, order.catalystPotency - 1) * 0.08f);
        potencyScale *= GetCatalystFamilyPotencyScale(
            order.catalystFamily);
        float variance = Mathf.Clamp(order.resultVariance, 0.01f, 0.5f);
        float rollScale = 1f
            + Mathf.Lerp(
                -variance,
                variance,
                random.NextFloat());
        bool stabilized = order.stabilizerAmount > 0;
        bool risky = !stabilized
            && (order.catalystFamily.IndexOf(
                    "offense",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || order.catalystFamily.IndexOf(
                    "arcane",
                    StringComparison.OrdinalIgnoreCase) >= 0);
        if (order.burdenSuppression
            && (string.IsNullOrWhiteSpace(order.suppressedBurdenEffectId)
                || string.Equals(
                    order.suppressedBurdenEffectId,
                    "equipment:risky",
                    StringComparison.Ordinal)))
        {
            risky = false;
        }
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

    public static float GetCatalystFamilyPotencyScale(string catalystFamily) =>
        EquipmentEvolutionRules.GetCatalystFamilyPotencyScale(catalystFamily);

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

}
