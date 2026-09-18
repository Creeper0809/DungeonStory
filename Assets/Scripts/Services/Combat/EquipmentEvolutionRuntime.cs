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
    private readonly IEquipmentEvolutionInputOwnerRuntime inputOwners;
    private readonly IFacilityEvolutionStateComponentFactory facilityStates;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IGameplayOutcomeNarrativeEvidenceQuery outcomeEvidenceQuery;
    private readonly IGameplayOutcomeEvidenceUseTransaction evidenceUseTransaction;
    private string lastAttunementNodeGenerationFailureFingerprint = string.Empty;

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
        IEquipmentEvolutionInputOwnerRuntime inputOwners,
        IFacilityEvolutionStateComponentFactory facilityStates,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IGameplayOutcomeNarrativeEvidenceQuery outcomeEvidenceQuery,
        IGameplayOutcomeEvidenceUseTransaction evidenceUseTransaction)
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
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.facilityStates = facilityStates
            ?? throw new ArgumentNullException(nameof(facilityStates));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.outcomeEvidenceQuery = outcomeEvidenceQuery
            ?? throw new ArgumentNullException(nameof(outcomeEvidenceQuery));
        this.evidenceUseTransaction = evidenceUseTransaction
            ?? throw new ArgumentNullException(nameof(evidenceUseTransaction));
    }

    public IReadOnlyList<EvolutionReforgeOrder> ReforgeOrders =>
        CurrentState.ReforgeOrders.Select(order => order.Clone()).ToArray();
    public IReadOnlyList<EquipmentReattunementOrder> ReattunementOrders =>
        CurrentState.ReattunementOrders.Select(order => order.Clone()).ToArray();
    public string LastAttunementNodeGenerationFailureForDiagnostics { get; private set; }
        = string.Empty;

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
        int repeatCount = 1,
        GameplayNarrativeEventContext narrativeContext = null)
    {
        LastAttunementNodeGenerationFailureForDiagnostics = string.Empty;
        CombatEquipmentInstance instance = RequireInstance(equipmentInstanceId);
        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        UsageLedgerEvent recorded = ledgerCompactor.Record(
            state.usageLedger,
            eventId,
            amount,
            ownerPersistentId,
            instance.instanceId,
            sourceTags,
            historicalEvidenceKind: historicalEvidenceKind,
            outcomeId: outcomeId,
            generation: state.generation,
            repeatCount: repeatCount,
            narrativeContext: narrativeContext);
        RecordFormulaEvidence(state, recorded);
        state.mastery = Mathf.Max(0f, state.mastery + Mathf.Max(0f, mastery));
        if (!string.IsNullOrWhiteSpace(ownerPersistentId)
            && attunementPoints > 0)
        {
            TryResolveFormulaTargetContext(
                instance,
                out EquipmentFormulaTargetContext targetContext,
                out string targetResolutionFailureReason);
            EquipmentAttunementAdvanceResult attunementResult = AddAttunement(
                state,
                targetContext,
                targetResolutionFailureReason,
                instance.instanceId,
                ownerPersistentId.Trim(),
                attunementPoints);
            LastAttunementNodeGenerationFailureForDiagnostics =
                attunementResult.NodeGenerationFailureReason;
            ReportBlockedAttunementNodeGeneration(
                instance,
                targetContext,
                attunementResult);
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
        int repeatCount = 1,
        GameplayNarrativeEventContext narrativeContext = null)
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
            repeatCount,
            narrativeContext);
        return true;
    }

    public bool TryCommitPresentation(
        string equipmentInstanceId,
        string presentationId,
        string displayName,
        string narrativeFlavor,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!equipment.TryGetInstance(equipmentInstanceId, out CombatEquipmentInstance instance))
        {
            failureReason = "장비 표현 요청의 소유 장비를 찾을 수 없습니다.";
            return false;
        }
        EquipmentEvolutionState previousState = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EquipmentEvolutionPresentationRequest request = state.presentationRequests?
            .SingleOrDefault(value => value != null && string.Equals(
                value.presentationId, presentationId?.Trim(), StringComparison.Ordinal));
        EvolutionNode node = request == null ? null : state.evolutionNodes?
            .SingleOrDefault(value => value != null && string.Equals(
                value.nodeId, request.nodeId, StringComparison.Ordinal));
        if (request == null
            || node == null
            || request.state != EquipmentEvolutionPresentationState.PresentationPending
            || node.presentationState != EquipmentEvolutionPresentationState.PresentationPending
            || !string.Equals(request.targetPersistentId, instance.instanceId, StringComparison.Ordinal)
            || !string.Equals(request.presentationId, node.presentationId, StringComparison.Ordinal))
        {
            failureReason = "장비 표현 요청이 없거나 이미 종료되었습니다.";
            return false;
        }
        string name = displayName?.Trim() ?? string.Empty;
        string flavor = narrativeFlavor?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > 32 || flavor.Length == 0 || flavor.Length > 180)
        {
            failureReason = "장비 표현 이름 또는 서사 길이가 허용 범위를 벗어났습니다.";
            return false;
        }
        if (ContainsMechanicalNumber(name) || ContainsMechanicalNumber(flavor))
        {
            failureReason = "장비 표현 문구에는 숫자 또는 퍼센트 표기를 넣을 수 없습니다.";
            return false;
        }

        EquipmentEvolutionFormulaCatalogSO catalog;
        try
        {
            catalog = EquipmentEvolutionFormulaCatalogSO.LoadForPersistedNode(node.formulaVersion);
            ValidateFormulaNode(node, catalog);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            failureReason = error.Message;
            return false;
        }
        string[] evidenceIds = request.evidenceIds?
            .OrderBy(value => value, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings =
            (node.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToArray();
        HashSet<string> exactIds = exactBindings.Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        if (!evidenceIds.SequenceEqual(
                node.evidenceIds.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            || !exactIds.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                (node.gameplayOutcomeEvidence
                        ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
                    .Where(value => value != null).Select(value => value.publicFactId)
                    .OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            failureReason = "장비 표현 요청의 근거가 동결된 공식과 다릅니다.";
            return false;
        }
        List<EquipmentEvolutionFormulaEvidenceRecord> evidence = evidenceIds
            .Where(id => !exactIds.Contains(id))
            .Select(id => state.formulaEvidence.SingleOrDefault(value => value != null
                && string.Equals(value.evidenceId, id, StringComparison.Ordinal)))
            .ToList();
        if (evidence.Any(value => value == null))
        {
            failureReason = "장비 표현 요청의 원본 근거를 찾을 수 없습니다.";
            return false;
        }

        foreach (EquipmentEvolutionFormulaEvidenceRecord item in evidence)
            item.influenceUseCount = checked(item.influenceUseCount + 1);
        node.displayName = name;
        node.narrativeFlavor = flavor;
        node.description = flavor;
        node.active = true;
        node.mechanicallyUnlocked = true;
        node.narrativeReady = true;
        node.uiVisible = true;
        node.playerVisible = true;
        node.presentationState = EquipmentEvolutionPresentationState.Ready;
        node.presentationFailureCount = request.failureCount;

        if (node.historical)
        {
            AttunementRecord attunement = state.attunements.SingleOrDefault(value => value != null
                && string.Equals(value.ownerPersistentId,
                    request.attunementOwnerPersistentId, StringComparison.Ordinal));
            if (attunement == null)
            {
                failureReason = "장비 귀속 기록의 소유자를 찾을 수 없습니다.";
                return false;
            }
            attunement.historyNodeIds ??= new List<string>();
            if (!attunement.historyNodeIds.Contains(node.nodeId, StringComparer.Ordinal))
                attunement.historyNodeIds.Add(node.nodeId);
            if (string.IsNullOrWhiteSpace(attunement.rootNodeId))
                attunement.rootNodeId = node.nodeId;
            state.activeHistoricalNodeIds ??= new List<string>();
            if (state.activeHistoricalNodeIds.Count < state.ResonanceBudget
                && !state.activeHistoricalNodeIds.Contains(node.nodeId, StringComparer.Ordinal))
                state.activeHistoricalNodeIds.Add(node.nodeId);
        }
        else if (!string.IsNullOrWhiteSpace(request.reforgeOrderId))
        {
            if (request.targetGeneration != state.generation + 1
                || request.masteryCost <= 0f
                || state.mastery + 0.001f < request.masteryCost)
            {
                failureReason = "장비 진화 비용 또는 세대가 동결된 요청과 다릅니다.";
                return false;
            }
            state.generation = request.targetGeneration;
            state.mastery -= request.masteryCost;
            state.reforgeReady = false;
            state.pendingHistoryHash = string.Empty;
            state.pendingDirection = EquipmentEvolutionDirection.Balanced;
        }
        state.presentationRequests.RemoveAll(value => value != null
            && string.Equals(value.presentationId, request.presentationId, StringComparison.Ordinal));
        PreparedGameplayOutcomeEvidenceUse prepared = null;
        if (exactBindings.Length > 0
            && (!evidenceUseTransaction.TryPrepareBindings(
                    "equipment-evolution",
                    request.presentationId,
                    exactBindings,
                    out prepared,
                    out failureReason)
                || !prepared.TryCommitAnchors(out failureReason)))
        {
            prepared?.Cancel();
            return false;
        }
        bool statePublished;
        try
        {
            statePublished = equipment.TryUpdateEvolutionState(instance.instanceId, state);
        }
        catch
        {
            if (prepared != null)
                prepared.TryRollbackAnchors(out _);
            throw;
        }
        if (!statePublished)
        {
            if (prepared != null && !prepared.TryRollbackAnchors(out string rollbackFailure))
                Debug.LogError("Equipment evidence rollback failed: " + rollbackFailure);
            failureReason = "장비 공식 표현 확정 상태를 물리 장비에 게시할 수 없습니다.";
            return false;
        }
        try
        {
            prepared?.CompleteOwnerCommit();
        }
        catch
        {
            if (!equipment.TryUpdateEvolutionState(instance.instanceId, previousState))
                Debug.LogError("Equipment presentation rollback could not restore owner state.");
            if (prepared != null)
                prepared.TryRollbackAnchors(out _);
            throw;
        }
        if (!string.IsNullOrWhiteSpace(request.reforgeOrderId))
        {
            EvolutionReforgeOrder order = orders.SingleOrDefault(value => value != null
                && string.Equals(value.orderId, request.reforgeOrderId, StringComparison.Ordinal));
            if (order != null)
            {
                order.state = EvolutionReforgeOrderState.Completed;
                order.completedWork = order.requiredWork;
            }
        }
        return true;
    }

    public bool TryCommitModuleSelection(
        string equipmentInstanceId,
        EquipmentEvolutionModuleSelectionResponseDto response,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (response == null || !response.Validate(out failureReason)
            || !equipment.TryGetInstance(equipmentInstanceId, out CombatEquipmentInstance instance))
        {
            if (failureReason.Length == 0)
                failureReason = "장비 모듈 선택 요청 또는 소유 장비를 찾을 수 없습니다.";
            return false;
        }
        EquipmentEvolutionState previousState = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EquipmentEvolutionPresentationRequest request = state.presentationRequests?
            .SingleOrDefault(value => value != null && string.Equals(
                value.presentationId, response.selectionId, StringComparison.Ordinal));
        EvolutionNode pending = request == null ? null : state.evolutionNodes?
            .SingleOrDefault(value => value != null && string.Equals(
                value.nodeId, request.nodeId, StringComparison.Ordinal));
        if (request == null || pending == null
            || request.state != EquipmentEvolutionPresentationState.ModuleSelectionPending
            || pending.presentationState != EquipmentEvolutionPresentationState.ModuleSelectionPending
            || !string.Equals(request.targetPersistentId, instance.instanceId, StringComparison.Ordinal)
            || !string.Equals(pending.moduleSelectionId, response.selectionId, StringComparison.Ordinal))
        {
            failureReason = "장비 모듈 선택 요청이 없거나 이미 종료되었습니다.";
            return false;
        }
        EquipmentEvolutionFormulaCatalogSO catalog;
        EvolutionNode node;
        try
        {
            catalog = EquipmentEvolutionFormulaCatalogSO.LoadForPersistedNode(pending.formulaVersion);
            NarrativeFormulaModuleSelectionRequest selectionRequest =
                EquipmentEvolutionRules.BuildModuleSelectionRequest(pending, catalog);
            NarrativeFormulaModuleSelectionChoice choice = new(
                response.selectionId,
                response.positiveModuleIds,
                response.drawbackModuleIds,
                response.evidenceFactIds);
            if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                    selectionRequest, choice, out _, out failureReason))
                return false;
            node = EquipmentEvolutionRules.FreezeSelectedModule(
                pending, state, catalog, instance.instanceId, choice);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            failureReason = error.Message;
            return false;
        }
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings =
            (node.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToArray();
        HashSet<string> exactIds = exactBindings.Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        List<EquipmentEvolutionFormulaEvidenceRecord> evidence = node.evidenceIds
            .Where(id => !exactIds.Contains(id))
            .Select(id => state.formulaEvidence.SingleOrDefault(value => value != null
                && string.Equals(value.evidenceId, id, StringComparison.Ordinal)))
            .ToList();
        if (evidence.Any(value => value == null))
        {
            failureReason = "장비 모듈 선택의 원본 근거를 찾을 수 없습니다.";
            return false;
        }
        foreach (EquipmentEvolutionFormulaEvidenceRecord item in evidence)
            item.influenceUseCount = checked(item.influenceUseCount + 1);
        node.displayName = response.displayName;
        node.narrativeFlavor = response.narrativeFlavor;
        node.description = response.narrativeFlavor;
        node.active = true;
        node.mechanicallyUnlocked = true;
        node.narrativeReady = true;
        node.uiVisible = true;
        node.playerVisible = true;
        node.presentationState = EquipmentEvolutionPresentationState.Ready;
        node.presentationFailureCount = request.failureCount;
        int nodeIndex = state.evolutionNodes.FindIndex(value => value != null
            && string.Equals(value.nodeId, node.nodeId, StringComparison.Ordinal));
        if (nodeIndex < 0)
        {
            failureReason = "장비 모듈 선택 노드를 다시 찾을 수 없습니다.";
            return false;
        }
        state.evolutionNodes[nodeIndex] = node;
        if (node.historical)
        {
            AttunementRecord attunement = state.attunements.SingleOrDefault(value => value != null
                && string.Equals(value.ownerPersistentId,
                    request.attunementOwnerPersistentId, StringComparison.Ordinal));
            if (attunement == null)
            {
                failureReason = "장비 귀속 기록의 소유자를 찾을 수 없습니다.";
                return false;
            }
            attunement.historyNodeIds ??= new List<string>();
            if (!attunement.historyNodeIds.Contains(node.nodeId, StringComparer.Ordinal))
                attunement.historyNodeIds.Add(node.nodeId);
            if (string.IsNullOrWhiteSpace(attunement.rootNodeId))
                attunement.rootNodeId = node.nodeId;
            state.activeHistoricalNodeIds ??= new List<string>();
            if (state.activeHistoricalNodeIds.Count < state.ResonanceBudget
                && !state.activeHistoricalNodeIds.Contains(node.nodeId, StringComparer.Ordinal))
                state.activeHistoricalNodeIds.Add(node.nodeId);
        }
        else if (!string.IsNullOrWhiteSpace(request.reforgeOrderId))
        {
            if (request.targetGeneration != state.generation + 1
                || request.masteryCost <= 0f
                || state.mastery + 0.001f < request.masteryCost)
            {
                failureReason = "장비 진화 비용 또는 세대가 동결된 요청과 다릅니다.";
                return false;
            }
            state.generation = request.targetGeneration;
            state.mastery -= request.masteryCost;
            state.reforgeReady = false;
            state.pendingHistoryHash = string.Empty;
            state.pendingDirection = EquipmentEvolutionDirection.Balanced;
        }
        state.presentationRequests.RemoveAll(value => value != null
            && string.Equals(value.presentationId, request.presentationId, StringComparison.Ordinal));
        try
        {
            EquipmentEvolutionRules.ValidateFormulaNode(node, catalog);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            failureReason = error.Message;
            return false;
        }
        PreparedGameplayOutcomeEvidenceUse prepared = null;
        if (exactBindings.Length > 0
            && (!evidenceUseTransaction.TryPrepareBindings(
                    "equipment-evolution",
                    request.presentationId,
                    exactBindings,
                    out prepared,
                    out failureReason)
                || !prepared.TryCommitAnchors(out failureReason)))
        {
            prepared?.Cancel();
            return false;
        }
        bool statePublished;
        try
        {
            statePublished = equipment.TryUpdateEvolutionState(instance.instanceId, state);
        }
        catch
        {
            if (prepared != null)
                prepared.TryRollbackAnchors(out _);
            throw;
        }
        if (!statePublished)
        {
            if (prepared != null && !prepared.TryRollbackAnchors(out string rollbackFailure))
                Debug.LogError("Equipment evidence rollback failed: " + rollbackFailure);
            failureReason = "장비 모듈 선택 확정 상태를 물리 장비에 게시할 수 없습니다.";
            return false;
        }
        try
        {
            prepared?.CompleteOwnerCommit();
        }
        catch
        {
            if (!equipment.TryUpdateEvolutionState(instance.instanceId, previousState))
                Debug.LogError("Equipment module-selection rollback could not restore owner state.");
            if (prepared != null)
                prepared.TryRollbackAnchors(out _);
            throw;
        }
        if (!string.IsNullOrWhiteSpace(request.reforgeOrderId))
        {
            EvolutionReforgeOrder order = orders.SingleOrDefault(value => value != null
                && string.Equals(value.orderId, request.reforgeOrderId, StringComparison.Ordinal));
            if (order != null)
            {
                order.state = EvolutionReforgeOrderState.Completed;
                order.completedWork = order.requiredWork;
            }
        }
        return true;
    }

    public bool TryRegisterPresentationFailure(
        string equipmentInstanceId,
        string presentationId,
        string reason,
        out bool awaitingNarrativeRetry)
    {
        awaitingNarrativeRetry = false;
        if (!equipment.TryGetInstance(equipmentInstanceId, out CombatEquipmentInstance instance))
            return false;
        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EquipmentEvolutionPresentationRequest request = state.presentationRequests?
            .SingleOrDefault(value => value != null && string.Equals(
                value.presentationId, presentationId?.Trim(), StringComparison.Ordinal));
        EvolutionNode node = request == null ? null : state.evolutionNodes?
            .SingleOrDefault(value => value != null && string.Equals(
                value.nodeId, request.nodeId, StringComparison.Ordinal));
        if (request == null || node == null
            || request.state is not EquipmentEvolutionPresentationState.PresentationPending
                and not EquipmentEvolutionPresentationState.ModuleSelectionPending)
            return false;
        request.failureCount = checked(request.failureCount + 1);
        request.lastFailureReason = reason?.Trim() ?? string.Empty;
        node.presentationFailureCount = request.failureCount;
        awaitingNarrativeRetry = request.failureCount >= 5;
        if (awaitingNarrativeRetry)
        {
            request.state = EquipmentEvolutionPresentationState.AwaitingNarrativeRetry;
            node.presentationState = EquipmentEvolutionPresentationState.AwaitingNarrativeRetry;
        }
        return equipment.TryUpdateEvolutionState(instance.instanceId, state);
    }

    public bool TryResumePresentation(
        string equipmentInstanceId,
        string presentationId)
    {
        if (!equipment.TryGetInstance(equipmentInstanceId, out CombatEquipmentInstance instance))
            return false;
        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EquipmentEvolutionPresentationRequest request = state.presentationRequests?
            .SingleOrDefault(value => value != null && string.Equals(
                value.presentationId, presentationId?.Trim(), StringComparison.Ordinal));
        EvolutionNode node = request == null ? null : state.evolutionNodes?
            .SingleOrDefault(value => value != null && string.Equals(
                value.nodeId, request.nodeId, StringComparison.Ordinal));
        if (request == null || node == null
            || request.state != EquipmentEvolutionPresentationState.AwaitingNarrativeRetry
            || node.presentationState != EquipmentEvolutionPresentationState.AwaitingNarrativeRetry)
            return false;
        request.failureCount = 0;
        request.lastFailureReason = string.Empty;
        request.state = node.formulaVersion >= EquipmentEvolutionRules.ModuleSelectionFormulaVersion
            && node.formulaCapabilities.Count == 0
            ? EquipmentEvolutionPresentationState.ModuleSelectionPending
            : EquipmentEvolutionPresentationState.PresentationPending;
        node.presentationFailureCount = 0;
        node.presentationState = request.state;
        return equipment.TryUpdateEvolutionState(instance.instanceId, state);
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
        if (CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
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

        if (!TryRequireRuntimeApplicableFormulaTarget(
                instance,
                out _,
                out failureReason))
        {
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
        if (!TryBuildInputOwnerDescriptor(
                created,
                requirements,
                out EquipmentEvolutionInputOwnerDescriptor descriptor,
                out failureReason)
            || !inputOwners.TryOpen(
                descriptor,
                out EquipmentEvolutionInputOwnerProjection projection,
                out failureReason))
        {
            return false;
        }
        ApplyInputOwnerProjection(created, projection);
        if (!TryBuildInputOwnerDescriptor(
                created,
                requirements,
                out descriptor,
                out failureReason)
            || !inputOwners.TryRequest(descriptor, out failureReason))
        {
            CloseInputOwnerOrThrow(
                created,
                requirements,
                "equipment-reforge-queue-rollback");
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

        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance targetInstance))
        {
            failureReason = "재단조 중인 장비가 사라졌습니다.";
            return false;
        }
        if (!TryRequireRuntimeApplicableFormulaTarget(
                targetInstance,
                out _,
                out failureReason))
        {
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

        EquipmentEvolutionPresentationRequest existingRequest =
            state.presentationRequests?.FirstOrDefault(value => value != null
                && string.Equals(value.reforgeOrderId, order.orderId, StringComparison.Ordinal));
        if (existingRequest != null)
        {
            completedNode = state.evolutionNodes.FirstOrDefault(value => value != null
                && string.Equals(value.nodeId, existingRequest.nodeId, StringComparison.Ordinal))?.Clone();
            return completedNode != null;
        }

        EvolutionNode node = BuildCompletedNode(instance, state, order);
        state.evolutionNodes.Add(node);
        state.presentationRequests ??= new List<EquipmentEvolutionPresentationRequest>();
        state.presentationRequests.Add(BuildPresentationRequest(
            node,
            instance.instanceId,
            order.lockedHistoryHash,
            string.Empty,
            order.orderId,
            order.targetGeneration,
            EquipmentEvolutionProgression.GetRequiredMastery(state.generation)));
        EquipmentEvolutionState previousState = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (!equipment.TryUpdateEvolutionState(instance.instanceId, state))
        {
            failureReason = "재단조 장비 상태를 게시할 수 없습니다.";
            return false;
        }
        if (!TryCloseInputOwner(
                order,
                BuildRequirements(order),
                "equipment-reforge-completed",
                out failureReason))
        {
            if (!equipment.TryUpdateEvolutionState(
                    instance.instanceId,
                    previousState))
            {
                throw new InvalidOperationException(
                    "Equipment reforge state rollback failed after input close rejection.");
            }
            return false;
        }
        // Work/material custody is complete. The physical equipment now owns the
        // durable presentation request, while mechanics and mastery stay pending.
        order.state = EvolutionReforgeOrderState.Completed;
        order.completedWork = order.requiredWork;
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

        if (!TryCloseInputOwner(
                order,
                BuildRequirements(order),
                "equipment-reforge-cancelled",
                out failureReason))
        {
            return false;
        }
        order.state = EvolutionReforgeOrderState.Cancelled;
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
        if (CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState))
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

        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [created.catalystItemId] = 1
        };
        if (!TryBuildInputOwnerDescriptor(
                created,
                requirements,
                out EquipmentEvolutionInputOwnerDescriptor descriptor,
                out failureReason)
            || !inputOwners.TryOpen(
                descriptor,
                out EquipmentEvolutionInputOwnerProjection projection,
                out failureReason))
        {
            return false;
        }
        ApplyInputOwnerProjection(created, projection);
        if (!TryBuildInputOwnerDescriptor(
                created,
                requirements,
                out descriptor,
                out failureReason)
            || !inputOwners.TryRequest(descriptor, out failureReason))
        {
            CloseInputOwnerOrThrow(
                created,
                requirements,
                "equipment-reattunement-queue-rollback");
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

        EquipmentEvolutionState previousState = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        state.activeHistoricalNodeIds =
            new List<string>(order.resultingActiveNodeIds);
        if (!equipment.TryUpdateEvolutionState(instance.instanceId, state))
        {
            failureReason = "재귀속 장비 상태를 게시할 수 없습니다.";
            return false;
        }
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [order.catalystItemId] = 1
        };
        if (!TryCloseInputOwner(
                order,
                requirements,
                "equipment-reattunement-completed",
                out failureReason))
        {
            if (!equipment.TryUpdateEvolutionState(
                    instance.instanceId,
                    previousState))
            {
                throw new InvalidOperationException(
                    "Equipment reattunement state rollback failed after input close rejection.");
            }
            return false;
        }
        order.state = EvolutionReforgeOrderState.Completed;
        order.completedWork = order.requiredWork;
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

        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [order.catalystItemId] = 1
        };
        if (!TryCloseInputOwner(
                order,
                requirements,
                "equipment-reattunement-cancelled",
                out failureReason))
        {
            return false;
        }
        order.state = EvolutionReforgeOrderState.Cancelled;
        return true;
    }

    public EquipmentEvolutionSaveData Capture()
    {
        ValidateInputOwnersBeforeCapture();
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
        EquipmentEvolutionAggregateState restored =
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State;
        if (!TryBuildInputOwnerDescriptors(
                restored.ReforgeOrders,
                restored.ReattunementOrders,
                out IReadOnlyList<EquipmentEvolutionInputOwnerDescriptor>
                    descriptors,
                out string failureReason)
            || !inputOwners.TryReplaceForRestore(
                descriptors,
                out failureReason))
        {
            throw new InvalidOperationException(
                "Equipment evolution input restore publication failed: "
                + failureReason);
        }
        aggregateRootStore.Replace(restored);
    }

    private bool EnsureMaterialsReady(
        EvolutionReforgeOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, int> requirements = BuildRequirements(order);
        if (!TryBuildInputOwnerDescriptor(
                order,
                requirements,
                out EquipmentEvolutionInputOwnerDescriptor descriptor,
                out failureReason)
            || !inputOwners.TryValidateAuthority(
                descriptor,
                out failureReason))
        {
            return false;
        }
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
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [order.catalystItemId] = 1
        };
        if (!TryBuildInputOwnerDescriptor(
                order,
                requirements,
                out EquipmentEvolutionInputOwnerDescriptor descriptor,
                out failureReason)
            || !inputOwners.TryValidateAuthority(
                descriptor,
                out failureReason))
        {
            return false;
        }
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

    private void ValidateInputOwnersBeforeCapture()
    {
        if (!TryBuildInputOwnerDescriptors(
                CurrentState.ReforgeOrders,
                CurrentState.ReattunementOrders,
                out IReadOnlyList<EquipmentEvolutionInputOwnerDescriptor>
                    descriptors,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Equipment evolution input capture projection failed: "
                + failureReason);
        }
        foreach (EquipmentEvolutionInputOwnerDescriptor descriptor in descriptors)
        {
            if (!inputOwners.TryValidateAuthority(
                    descriptor,
                    out failureReason))
            {
                throw new InvalidOperationException(
                    "Equipment evolution input authority is invalid for order '"
                    + descriptor.OrderId + "': " + failureReason);
            }
        }
    }

    private bool TryBuildInputOwnerDescriptors(
        IEnumerable<EvolutionReforgeOrder> reforge,
        IEnumerable<EquipmentReattunementOrder> reattunement,
        out IReadOnlyList<EquipmentEvolutionInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        List<EquipmentEvolutionInputOwnerDescriptor> result = new();
        foreach (EvolutionReforgeOrder order in
                 (reforge ?? Array.Empty<EvolutionReforgeOrder>())
                 .Where(value => value != null
                     && value.state is not EvolutionReforgeOrderState.Completed
                         and not EvolutionReforgeOrderState.Cancelled)
                 .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            if (!TryBuildInputOwnerDescriptor(
                    order,
                    BuildRequirements(order),
                    out EquipmentEvolutionInputOwnerDescriptor descriptor,
                    out failureReason))
            {
                descriptors = null;
                return false;
            }
            result.Add(descriptor);
        }
        foreach (EquipmentReattunementOrder order in
                 (reattunement ?? Array.Empty<EquipmentReattunementOrder>())
                 .Where(value => value != null
                     && value.state is not EvolutionReforgeOrderState.Completed
                         and not EvolutionReforgeOrderState.Cancelled)
                 .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            Dictionary<string, int> requirements = new(StringComparer.Ordinal)
            {
                [order.catalystItemId] = 1
            };
            if (!TryBuildInputOwnerDescriptor(
                    order,
                    requirements,
                    out EquipmentEvolutionInputOwnerDescriptor descriptor,
                    out failureReason))
            {
                descriptors = null;
                return false;
            }
            result.Add(descriptor);
        }
        descriptors = result
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        failureReason = string.Empty;
        return true;
    }

    private bool TryBuildInputOwnerDescriptor(
        EvolutionReforgeOrder order,
        IReadOnlyDictionary<string, int> requirements,
        out EquipmentEvolutionInputOwnerDescriptor descriptor,
        out string failureReason) => TryBuildInputOwnerDescriptor(
        order?.orderId,
        order?.destinationId,
        order?.facilityPersistentId,
        order == null
            ? default
            : new Vector2Int(order.destinationX, order.destinationY),
        order?.equipmentInstanceId,
        order?.inputBufferCapacityGrams ?? 0L,
        order?.inputMassAuthorityRevision ?? 0L,
        order?.inputCapacityFingerprint,
        requirements,
        out descriptor,
        out failureReason);

    private bool TryBuildInputOwnerDescriptor(
        EquipmentReattunementOrder order,
        IReadOnlyDictionary<string, int> requirements,
        out EquipmentEvolutionInputOwnerDescriptor descriptor,
        out string failureReason) => TryBuildInputOwnerDescriptor(
        order?.orderId,
        order?.destinationId,
        order?.facilityPersistentId,
        order == null
            ? default
            : new Vector2Int(order.destinationX, order.destinationY),
        order?.equipmentInstanceId,
        order?.inputBufferCapacityGrams ?? 0L,
        order?.inputMassAuthorityRevision ?? 0L,
        order?.inputCapacityFingerprint,
        requirements,
        out descriptor,
        out failureReason);

    private bool TryBuildInputOwnerDescriptor(
        string orderId,
        string destinationId,
        string facilityPersistentId,
        Vector2Int position,
        string equipmentInstanceId,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint,
        IReadOnlyDictionary<string, int> requirements,
        out EquipmentEvolutionInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        descriptor = null;
        failureReason = string.Empty;
        try
        {
            if (!equipment.TryGetInstance(
                    equipmentInstanceId,
                    out CombatEquipmentInstance instance)
                || string.IsNullOrWhiteSpace(instance.sourceStackId))
            {
                failureReason = "equipment-evolution-input-instance-missing";
                return false;
            }
            string equipmentItemId = PhysicalItemIds.ForEquipment(
                instance.definitionId);
            WorldItemStackSnapshot[] sourceStacks = worldItems.GetAllStacks()
                .Where(value => value != null && string.Equals(
                    value.StackId,
                    instance.sourceStackId,
                    StringComparison.Ordinal))
                .ToArray();
            if (sourceStacks.Length != 1
                || sourceStacks[0].Quantity != 1
                || !string.Equals(
                    sourceStacks[0].ItemId,
                    equipmentItemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    sourceStacks[0].ItemInstanceId,
                    instance.instanceId,
                    StringComparison.Ordinal)
                || !equipment.TryGetInstanceBySourceStack(
                    instance.sourceStackId,
                    out CombatEquipmentInstance linked)
                || !string.Equals(
                    linked.instanceId,
                    instance.instanceId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "equipment-evolution-input-unique-equipment-custody-invalid";
                return false;
            }

            Dictionary<string, EquipmentModuleInstance> modulesById =
                (equipment.ModuleInstances
                    ?? Array.Empty<EquipmentModuleInstance>())
                .Where(value => value != null
                    && !string.IsNullOrWhiteSpace(value.instanceId))
                .ToDictionary(
                    value => value.instanceId,
                    value => value,
                    StringComparer.Ordinal);
            List<EquipmentModuleInstance> attachedModules = new();
            HashSet<int> slotIndexes = new();
            HashSet<string> moduleIds = new(StringComparer.Ordinal);
            foreach (EquipmentModuleSlotState slot in
                     (instance.moduleSlots
                         ?? new List<EquipmentModuleSlotState>())
                     .Where(value => value != null
                         && !string.IsNullOrWhiteSpace(value.moduleInstanceId))
                     .OrderBy(value => value.slotIndex)
                     .ThenBy(value => value.moduleInstanceId,
                         StringComparer.Ordinal))
            {
                if (!slotIndexes.Add(slot.slotIndex)
                    || !moduleIds.Add(slot.moduleInstanceId)
                    || !modulesById.TryGetValue(
                        slot.moduleInstanceId,
                        out EquipmentModuleInstance module)
                    || module.state != EquipmentModuleProcessState.Installed
                    || !string.Equals(
                        module.attachedEquipmentInstanceId,
                        instance.instanceId,
                        StringComparison.Ordinal))
                {
                    failureReason =
                        "equipment-evolution-input-attached-module-invalid";
                    return false;
                }
                attachedModules.Add(module);
            }
            ItemInstanceComponentSaveData expectedComponent =
                EquipmentItemStateCodec.Encode(instance, attachedModules);
            ItemInstanceComponentSaveData[] actualEquipmentComponents =
                (sourceStacks[0].Components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(value => value != null && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.Equipment,
                    StringComparison.Ordinal))
                .ToArray();
            if (actualEquipmentComponents.Length != 1
                || !string.Equals(
                    actualEquipmentComponents[0].ToCanonicalString(),
                    expectedComponent.ToCanonicalString(),
                    StringComparison.Ordinal))
            {
                failureReason =
                    "equipment-evolution-input-equipment-component-drift";
                return false;
            }

            descriptor = new EquipmentEvolutionInputOwnerDescriptor(
                orderId,
                destinationId,
                facilityPersistentId,
                position,
                instance.instanceId,
                equipmentItemId,
                instance.sourceStackId,
                sourceStacks[0].Components,
                requirements,
                storedCapacityGrams,
                storedMassAuthorityRevision,
                storedCapacityFingerprint);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "equipment-evolution-input-descriptor-invalid:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private static void ApplyInputOwnerProjection(
        EvolutionReforgeOrder order,
        EquipmentEvolutionInputOwnerProjection projection)
    {
        order.inputBufferCapacityGrams = projection.CapacityGrams;
        order.inputMassAuthorityRevision = projection.MassAuthorityRevision;
        order.inputCapacityFingerprint = projection.CapacityFingerprint;
    }

    private static void ApplyInputOwnerProjection(
        EquipmentReattunementOrder order,
        EquipmentEvolutionInputOwnerProjection projection)
    {
        order.inputBufferCapacityGrams = projection.CapacityGrams;
        order.inputMassAuthorityRevision = projection.MassAuthorityRevision;
        order.inputCapacityFingerprint = projection.CapacityFingerprint;
    }

    private bool TryCloseInputOwner(
        EvolutionReforgeOrder order,
        IReadOnlyDictionary<string, int> requirements,
        string reasonCode,
        out string failureReason) =>
        TryBuildInputOwnerDescriptor(
            order,
            requirements,
            out EquipmentEvolutionInputOwnerDescriptor descriptor,
            out failureReason)
        && inputOwners.TryClose(descriptor, reasonCode, out failureReason);

    private bool TryCloseInputOwner(
        EquipmentReattunementOrder order,
        IReadOnlyDictionary<string, int> requirements,
        string reasonCode,
        out string failureReason) =>
        TryBuildInputOwnerDescriptor(
            order,
            requirements,
            out EquipmentEvolutionInputOwnerDescriptor descriptor,
            out failureReason)
        && inputOwners.TryClose(descriptor, reasonCode, out failureReason);

    private void CloseInputOwnerOrThrow(
        EvolutionReforgeOrder order,
        IReadOnlyDictionary<string, int> requirements,
        string reasonCode)
    {
        if (!TryCloseInputOwner(
                order,
                requirements,
                reasonCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Equipment reforge input rollback failed: " + failureReason);
        }
    }

    private void CloseInputOwnerOrThrow(
        EquipmentReattunementOrder order,
        IReadOnlyDictionary<string, int> requirements,
        string reasonCode)
    {
        if (!TryCloseInputOwner(
                order,
                requirements,
                reasonCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Equipment reattunement input rollback failed: "
                + failureReason);
        }
    }

    private bool HasActiveEquipmentOrder(string equipmentInstanceId)
    {
        string normalized = equipmentInstanceId?.Trim() ?? string.Empty;
        if (equipment.TryGetInstance(normalized, out CombatEquipmentInstance instance)
            && instance.evolution?.presentationRequests?.Any(request => request != null
                && request.state is EquipmentEvolutionPresentationState.PresentationPending
                    or EquipmentEvolutionPresentationState.AwaitingNarrativeRetry) == true)
        {
            return true;
        }
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
        string parentNodeId = state.evolutionNodes
            .Where(node => node != null && !node.historical)
            .OrderByDescending(node => node.generation)
            .ThenBy(node => node.nodeId, StringComparer.Ordinal)
            .Select(node => node.nodeId)
            .FirstOrDefault() ?? string.Empty;
        string nodeHash = StableEvolutionHash.Compute(
            instance.instanceId + "|" + order.orderId);
        return BuildFormulaNode(
            state,
            EquipmentEvolutionFormulaCatalogSO.LoadRequired(),
            RequireFormulaTargetContext(instance),
            instance.instanceId,
            $"equipment-node:{nodeHash}",
            parentNodeId,
            order.lockedHistoryHash,
            order.lockedDirection,
            order.catalystFamily,
            order.targetGeneration,
            historical: false,
            manifestationPosition: $"reforge:{order.targetGeneration}",
            outcomeBindings: GameplayOutcomeEvidenceFormulaProjection.CaptureExact(
                outcomeEvidenceQuery.GetForEntity(
                    new GameplayEntityId(
                        new GameplayEntityKindId("item-instance"),
                        instance.instanceId),
                    maximumCount: 32,
                    minimumSalience: 0f,
                    includeCompacted: false)));
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

    private static bool ContainsMechanicalNumber(string value)
    {
        return (value ?? string.Empty).Any(character =>
            char.IsDigit(character) || character is '%' or '％');
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

    private EquipmentFormulaTargetContext RequireFormulaTargetContext(
        CombatEquipmentInstance instance)
    {
        if (instance == null
            || string.IsNullOrWhiteSpace(instance.definitionId)
            || !equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || definition == null
            || !string.Equals(
                definition.EquipmentId,
                instance.definitionId.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Equipment formula generation requires the instance's canonical catalog definition.");
        }

        return new EquipmentFormulaTargetContext(definition);
    }

    private bool TryResolveFormulaTargetContext(
        CombatEquipmentInstance instance,
        out EquipmentFormulaTargetContext targetContext,
        out string failureReason)
    {
        targetContext = null;
        failureReason = string.Empty;
        try
        {
            targetContext = RequireFormulaTargetContext(instance);
            return true;
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException or KeyNotFoundException)
        {
            failureReason = error.Message;
            return false;
        }
    }

    private void ReportBlockedAttunementNodeGeneration(
        CombatEquipmentInstance instance,
        EquipmentFormulaTargetContext targetContext,
        EquipmentAttunementAdvanceResult result)
    {
        if (!result.NodeGenerationBlocked)
        {
            lastAttunementNodeGenerationFailureFingerprint = string.Empty;
            return;
        }

        string instanceId = instance?.instanceId?.Trim() ?? string.Empty;
        string definitionId = targetContext?.DefinitionId
            ?? instance?.definitionId?.Trim()
            ?? string.Empty;
        string fingerprint = string.Join("|",
            instanceId,
            definitionId,
            result.BlockedTier,
            result.NodeGenerationFailureReason);
        if (string.Equals(
                lastAttunementNodeGenerationFailureFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            return;
        }

        lastAttunementNodeGenerationFailureFingerprint = fingerprint;
        Debug.LogWarning(
            "Equipment attunement node generation blocked for instance '"
            + instanceId + "', definition '" + definitionId + "', tier "
            + result.BlockedTier + ": " + result.NodeGenerationFailureReason);
    }

    private bool TryRequireRuntimeApplicableFormulaTarget(
        CombatEquipmentInstance instance,
        out EquipmentFormulaTargetContext targetContext,
        out string failureReason)
    {
        if (!TryResolveFormulaTargetContext(
                instance, out targetContext, out failureReason))
        {
            failureReason = "이 장비에는 실제로 적용되는 진화 효과가 없어 새 진화를 만들 수 없습니다. "
                + failureReason;
            return false;
        }
        if (!TryGetRuntimeApplicablePositiveOfferFailure(
                EquipmentEvolutionFormulaCatalogSO.LoadRequired(),
                targetContext,
                out failureReason))
        {
            failureReason = "이 장비에는 실제로 적용되는 진화 효과가 없어 새 진화를 만들 수 없습니다. "
                + failureReason;
            return false;
        }
        return true;
    }

}
