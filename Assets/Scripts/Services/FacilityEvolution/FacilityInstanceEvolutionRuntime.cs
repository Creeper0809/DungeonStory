using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class FacilityInstanceEvolutionRuntime : IFacilityEvolutionRuntime
{
    private const string BindingItemId = "resource:dark-resin";
    private static readonly string[] CatalystFamilies =
    {
        "catalyst:offense",
        "catalyst:defense",
        "catalyst:industry",
        "catalyst:survival",
        "catalyst:arcane",
        "catalyst:authority"
    };

    private readonly IFacilityEvolutionStateComponentFactory stateFactory;
    private readonly IUsageLedgerCompactor ledgerCompactor;
    private readonly IEvolutionModuleRegistry moduleRegistry;
    private readonly IRoomEnvironmentQuery roomEnvironment;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IFacilityRelocationWorldService relocationWorld;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;

    public FacilityInstanceEvolutionRuntime(
        IFacilityEvolutionStateComponentFactory stateFactory,
        IUsageLedgerCompactor ledgerCompactor,
        IEvolutionModuleRegistry moduleRegistry,
        IRoomEnvironmentQuery roomEnvironment,
        IFacilityCandidateCache facilityCandidateCache,
        IWorldItemStackRuntime worldItems,
        IFacilityRelocationWorldService relocationWorld,
        IRunSeedProvider runSeedProvider)
    {
        this.stateFactory = stateFactory
            ?? throw new ArgumentNullException(nameof(stateFactory));
        this.ledgerCompactor = ledgerCompactor
            ?? throw new ArgumentNullException(nameof(ledgerCompactor));
        this.moduleRegistry = moduleRegistry
            ?? throw new ArgumentNullException(nameof(moduleRegistry));
        this.roomEnvironment = roomEnvironment;
        this.facilityCandidateCache = facilityCandidateCache;
        this.worldItems = worldItems;
        this.relocationWorld = relocationWorld
            ?? throw new ArgumentNullException(nameof(relocationWorld));
        this.runSeedProvider = runSeedProvider
            ?? throw new ArgumentNullException(nameof(runSeedProvider));
        batchDispositions = null;
    }

    [VContainer.Inject]
    public FacilityInstanceEvolutionRuntime(
        IFacilityEvolutionStateComponentFactory stateFactory,
        IUsageLedgerCompactor ledgerCompactor,
        IEvolutionModuleRegistry moduleRegistry,
        IRoomEnvironmentQuery roomEnvironment,
        IFacilityCandidateCache facilityCandidateCache,
        IWorldItemStackRuntime worldItems,
        IFacilityRelocationWorldService relocationWorld,
        IRunSeedProvider runSeedProvider,
        IPhysicalItemBatchDispositionService batchDispositions)
    {
        this.stateFactory = stateFactory
            ?? throw new ArgumentNullException(nameof(stateFactory));
        this.ledgerCompactor = ledgerCompactor
            ?? throw new ArgumentNullException(nameof(ledgerCompactor));
        this.moduleRegistry = moduleRegistry
            ?? throw new ArgumentNullException(nameof(moduleRegistry));
        this.roomEnvironment = roomEnvironment;
        this.facilityCandidateCache = facilityCandidateCache;
        this.worldItems = worldItems;
        this.relocationWorld = relocationWorld
            ?? throw new ArgumentNullException(nameof(relocationWorld));
        this.runSeedProvider = runSeedProvider
            ?? throw new ArgumentNullException(nameof(runSeedProvider));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
    }

    public FacilityEvolutionState GetState(BuildableObject facility)
    {
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        EnsureNarrativeSnapshot(state);
        EnsureCandidates(facility, component, state);
        component.ReplaceInstanceEvolution(state);
        return component.InstanceEvolution;
    }

    public FacilityEvolutionState RecordUsage(
        BuildableObject facility,
        string eventId,
        float mastery,
        float amount = 1f,
        string actorId = "",
        IEnumerable<string> sourceTags = null)
    {
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        ledgerCompactor.Record(
            state.usageLedger,
            eventId,
            amount,
            actorId,
            state.facilityPersistentId,
            sourceTags);
        state.mastery = Mathf.Max(0f, state.mastery + Mathf.Max(0f, mastery));
        EnsureNarrativeSnapshot(state);
        EnsureCandidates(facility, component, state);
        component.ReplaceInstanceEvolution(state);
        facilityCandidateCache?.MarkDynamicStateDirty();
        return component.InstanceEvolution;
    }

    public IReadOnlyList<FacilityGenerationCandidate> GetGenerationCandidates(
        BuildableObject facility)
    {
        FacilityEvolutionState state = GetState(facility);
        return Array.AsReadOnly(state.pendingCandidates
            .Where(candidate => candidate != null)
            .Select(candidate => candidate.Clone())
            .ToArray());
    }

    public bool TryQueueCandidate(
        BuildableObject facility,
        string candidateId,
        out FacilityModificationOrder order,
        out string failureReason)
    {
        FacilityGenerationCandidate candidate = GetGenerationCandidates(facility)
            .FirstOrDefault(entry => entry != null
                && string.Equals(
                    entry.candidateId,
                    candidateId?.Trim(),
                    StringComparison.Ordinal));
        string catalystItemId = candidate != null
            && candidate.minimumCatalystProgressionLevel > 0
                ? EvolutionCatalystItemId.BuildCatalyst(
                    candidate.catalystFamily,
                    candidate.minimumCatalystProgressionLevel)
                : string.Empty;
        return TryQueueCandidate(
            facility,
            candidateId,
            catalystItemId,
            out order,
            out failureReason);
    }

    public bool TryQueueCandidate(
        BuildableObject facility,
        string candidateId,
        string catalystItemId,
        out FacilityModificationOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        EnsureCandidates(facility, component, state);
        FacilityGenerationCandidate candidate = state.pendingCandidates
            .FirstOrDefault(entry => entry != null
                && string.Equals(
                    entry.candidateId,
                    candidateId?.Trim(),
                    StringComparison.Ordinal));
        if (candidate == null)
        {
            failureReason = "선택한 시설 개조 후보를 찾을 수 없습니다.";
            return false;
        }

        if (!state.ReadyForGeneration)
        {
            failureReason =
                $"시설 숙련도가 부족합니다. {state.mastery:0.#}/{state.RequiredMastery:0.#}";
            return false;
        }

        if (state.modificationOrder != null
            || state.recalibrationOrder != null
            || state.relocationOrder != null)
        {
            failureReason = "이 시설에는 이미 개조 작업이 진행 중입니다.";
            return false;
        }

        FacilityGenerationCandidate lockedCandidate = candidate.Clone();
        string normalizedCatalystItemId = string.Empty;
        if (candidate.minimumCatalystProgressionLevel > 0)
        {
            if (!EvolutionCatalystItemId.TryParseCatalyst(
                    catalystItemId,
                    out EquipmentCatalystDefinition catalyst))
            {
                failureReason = "고위험 개조에는 재단조 촉매가 필요합니다.";
                return false;
            }

            if (catalyst.progressionLevel
                < candidate.minimumCatalystProgressionLevel)
            {
                failureReason =
                    $"촉매 진행 단계가 부족합니다. {catalyst.progressionLevel}/{candidate.minimumCatalystProgressionLevel}";
                return false;
            }

            normalizedCatalystItemId = catalyst.itemId;
            lockedCandidate.catalystFamily = catalyst.family;
            lockedCandidate.minimumCatalystProgressionLevel =
                catalyst.progressionLevel;
            lockedCandidate.benefitModuleId =
                FacilityEvolutionRules.ResolveFacilityModuleForCatalyst(
                    catalyst.family);
            lockedCandidate.burdenModuleId = "facility:risky-overdrive";
        }

        string orderId = $"facility-modification:{Guid.NewGuid():N}";
        string destinationId = $"facility-evolution:{orderId}";
        Vector2Int position = facility.centerPos;
        FacilityModificationOrder created = new FacilityModificationOrder
        {
            orderId = orderId,
            facilityPersistentId = state.facilityPersistentId,
            candidate = lockedCandidate,
            bindingItemId = BindingItemId,
            bindingAmount = 1,
            catalystItemId = normalizedCatalystItemId,
            catalystAmount = string.IsNullOrWhiteSpace(normalizedCatalystItemId)
                ? 0
                : 1,
            requiredWork = FacilityEvolutionProgression.GetModificationWork(
                facility.BuildingData.GetRequiredWork(
                    BuiltInWorkTypeIds.Construct),
                state.generation),
            state = EvolutionReforgeOrderState.WaitingForMaterials,
            destinationId = destinationId,
            destinationX = position.x,
            destinationY = position.y
        };
        if (!RequestMaterials(
                position,
                destinationId,
                FacilityEvolutionRules.BuildRequirements(created),
                out failureReason))
        {
            return false;
        }

        state.modificationOrder = created;
        component.ReplaceInstanceEvolution(state);
        facilityCandidateCache?.MarkDynamicStateDirty();
        order = created.Clone();
        return true;
    }

    public bool TryQueueRecalibration(
        BuildableObject facility,
        string nodeId,
        EvolutionModuleActivationRule targetRule,
        string catalystItemId,
        out FacilityRecalibrationOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        if (state.modificationOrder != null
            || state.recalibrationOrder != null
            || state.relocationOrder != null)
        {
            failureReason = "이 시설에는 이미 개조 작업이 진행 중입니다.";
            return false;
        }

        EvolutionNode node = state.evolutionNodes.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.nodeId,
                nodeId?.Trim(),
                StringComparison.Ordinal));
        if (node == null)
        {
            failureReason = "재조율할 진화 노드를 찾을 수 없습니다.";
            return false;
        }

        if (!EvolutionCatalystItemId.TryParseCatalyst(
                catalystItemId,
                out EquipmentCatalystDefinition catalyst))
        {
            failureReason = "재조율에는 촉매 한 개가 필요합니다.";
            return false;
        }

        int requiredProgressionLevel =
            EquipmentEvolutionProgression.GetMinimumCatalystProgressionLevel(
                Mathf.Max(0, node.generation - 1));
        if (catalyst.progressionLevel < requiredProgressionLevel)
        {
            failureReason =
                $"촉매 진행 단계가 부족합니다. {catalyst.progressionLevel}/{requiredProgressionLevel}";
            return false;
        }

        string orderId = $"facility-recalibration:{Guid.NewGuid():N}";
        string destinationId = $"facility-evolution:{orderId}";
        Vector2Int position = facility.centerPos;
        FacilityRecalibrationOrder created = new FacilityRecalibrationOrder
        {
            orderId = orderId,
            facilityPersistentId = state.facilityPersistentId,
            nodeId = node.nodeId,
            targetRule = targetRule?.Clone()
                ?? new EvolutionModuleActivationRule(),
            catalystItemId = catalyst.itemId,
            catalystPotency = catalyst.potency,
            requiredWork = FacilityEvolutionProgression.GetRecalibrationWork(
                facility.BuildingData.GetRequiredWork(
                    BuiltInWorkTypeIds.Construct),
                state.generation),
            state = EvolutionReforgeOrderState.WaitingForMaterials,
            destinationId = destinationId,
            destinationX = position.x,
            destinationY = position.y
        };
        if (!RequestMaterials(
                position,
                destinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [created.catalystItemId] = 1
                },
                out failureReason))
        {
            return false;
        }

        state.recalibrationOrder = created;
        component.ReplaceInstanceEvolution(state);
        facilityCandidateCache?.MarkDynamicStateDirty();
        order = created.Clone();
        return true;
    }

    public bool TryQueueRecalibrationToCurrentRoom(
        BuildableObject facility,
        string nodeId,
        string catalystItemId,
        out FacilityRecalibrationOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        if (facility == null || facility.isDestroy)
        {
            failureReason = "재조율할 시설을 찾을 수 없습니다.";
            return false;
        }

        return TryQueueRecalibration(
            facility,
            nodeId,
            BuildCurrentRoomRule(facility),
            catalystItemId,
            out order,
            out failureReason);
    }

    public bool TryQueueRelocation(
        BuildableObject facility,
        Vector2Int destination,
        out FacilityRelocationOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        if (state.modificationOrder != null
            || state.recalibrationOrder != null
            || state.relocationOrder != null)
        {
            failureReason = "이 시설에는 이미 개조나 이전 작업이 진행 중입니다.";
            return false;
        }

        if (!relocationWorld.CanRelocate(
                facility,
                destination,
                out failureReason))
        {
            return false;
        }

        float baseWork = facility.BuildingData.GetRequiredWork(
            BuiltInWorkTypeIds.Construct);
        string orderId = $"facility-relocation:{Guid.NewGuid():N}";
        FacilityRelocationOrder created = new FacilityRelocationOrder
        {
            orderId = orderId,
            facilityPersistentId = state.facilityPersistentId,
            packageItemId =
                EvolutionCatalystItemDefinitions.FacilityPackageItemId,
            destinationId =
                WorldItemStackRuntime.FacilityInputDestinationPrefix
                + "relocation:"
                + orderId,
            sourceX = facility.centerPos.x,
            sourceY = facility.centerPos.y,
            destinationX = destination.x,
            destinationY = destination.y,
            dismantleRequiredWork =
                FacilityEvolutionProgression.GetRelocationDismantleWork(
                    baseWork),
            reinstallRequiredWork =
                FacilityEvolutionProgression.GetRelocationReinstallWork(
                    baseWork),
            phase = FacilityRelocationPhase.Dismantling
        };
        state.relocationOrder = created;
        component.ReplaceInstanceEvolution(state);
        facilityCandidateCache?.MarkDynamicStateDirty();
        order = created.Clone();
        return true;
    }

    public bool TryGetPendingWork(
        BuildableObject facility,
        out FacilityModificationOrder modification,
        out FacilityRecalibrationOrder recalibration)
    {
        FacilityEvolutionState state = GetState(facility);
        modification = state.modificationOrder?.Clone();
        recalibration = state.recalibrationOrder?.Clone();
        return modification != null || recalibration != null;
    }

    public bool TryGetPendingRelocation(
        BuildableObject facility,
        out FacilityRelocationOrder relocation)
    {
        relocation = null;
        if (facility == null || facility.isDestroy)
        {
            return false;
        }

        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        FacilityRelocationOrder order = state.relocationOrder;
        if (order == null)
        {
            return false;
        }

        if (order.phase == FacilityRelocationPhase.WaitingForPackage
            && (!order.packageConsumed
                || !string.IsNullOrEmpty(order.packageTransferOperationId))
            && FacilityRelocationPackageOutbox.TryCommitOrFinalize(
                order,batchDispositions,out _))
        {
            order.packageConsumed = true;
            order.phase = FacilityRelocationPhase.Reinstalling;
            component.ReplaceInstanceEvolution(state);
            facilityCandidateCache?.MarkDynamicStateDirty();
        }

        relocation = order.Clone();
        return relocation.phase == FacilityRelocationPhase.Dismantling
            || relocation.phase == FacilityRelocationPhase.Reinstalling;
    }

    public bool ApplyPendingWork(
        BuildableObject facility,
        float workUnits,
        out EvolutionNode completedNode,
        out bool completed,
        out string failureReason)
    {
        completedNode = null;
        completed = false;
        failureReason = string.Empty;
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        if (state.modificationOrder != null)
        {
            FacilityModificationOrder order = state.modificationOrder;
            if (!EnsureMaterialsReady(order, out failureReason))
            {
                component.ReplaceInstanceEvolution(state);
                return false;
            }

            order.state = EvolutionReforgeOrderState.InProgress;
            order.completedWork = Mathf.Clamp(
                order.completedWork + Mathf.Max(0f, workUnits),
                0f,
                order.requiredWork);
            if (order.completedWork + 0.001f < order.requiredWork)
            {
                component.ReplaceInstanceEvolution(state);
                return true;
            }

            if (!TryApplyCandidateNow(
                    facility,
                    component,
                    state,
                    order.candidate,
                    out completedNode,
                    out failureReason))
            {
                order.state = EvolutionReforgeOrderState.Blocked;
                component.ReplaceInstanceEvolution(state);
                return false;
            }

            FacilityEvolutionState completedState = component.InstanceEvolution;
            completedState.modificationOrder = null;
            component.ReplaceInstanceEvolution(completedState);
            completed = true;
            ReleaseDestination(order.destinationId, facility.centerPos);
            return true;
        }

        if (state.recalibrationOrder != null)
        {
            FacilityRecalibrationOrder order = state.recalibrationOrder;
            if (!EnsureMaterialsReady(order, out failureReason))
            {
                component.ReplaceInstanceEvolution(state);
                return false;
            }

            order.state = EvolutionReforgeOrderState.InProgress;
            order.completedWork = Mathf.Clamp(
                order.completedWork + Mathf.Max(0f, workUnits),
                0f,
                order.requiredWork);
            if (order.completedWork + 0.001f < order.requiredWork)
            {
                component.ReplaceInstanceEvolution(state);
                return true;
            }

            EvolutionNode node = state.evolutionNodes.FirstOrDefault(entry =>
                entry != null
                && string.Equals(
                    entry.nodeId,
                    order.nodeId,
                    StringComparison.Ordinal));
            if (node == null)
            {
                order.state = EvolutionReforgeOrderState.Blocked;
                component.ReplaceInstanceEvolution(state);
                failureReason = "재조율 대상 노드가 사라졌습니다.";
                return false;
            }

            node.activationRule = order.targetRule?.Clone()
                ?? new EvolutionModuleActivationRule();
            completedNode = node.Clone();
            state.recalibrationOrder = null;
            state.roomStructureVersion = -1;
            state.facilityStateVersion = -1;
            component.ReplaceInstanceEvolution(state);
            RefreshRoomActivation(facility);
            facilityCandidateCache?.MarkDynamicStateDirty();
            completed = true;
            ReleaseDestination(order.destinationId, facility.centerPos);
            return true;
        }

        failureReason = "진행할 시설 개조 작업이 없습니다.";
        return false;
    }

    public bool ApplyRelocationWork(
        BuildableObject facility,
        float workUnits,
        out BuildableObject relocatedFacility,
        out bool completed,
        out string failureReason)
    {
        relocatedFacility = null;
        completed = false;
        failureReason = string.Empty;
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        FacilityRelocationOrder order = state.relocationOrder;
        if (order == null)
        {
            failureReason = "진행할 시설 이전 작업이 없습니다.";
            return false;
        }

        if (order.phase == FacilityRelocationPhase.Dismantling)
        {
            order.dismantleCompletedWork = Mathf.Clamp(
                order.dismantleCompletedWork + Mathf.Max(0f, workUnits),
                0f,
                order.dismantleRequiredWork);
            if (order.dismantleCompletedWork + 0.001f
                < order.dismantleRequiredWork)
            {
                component.ReplaceInstanceEvolution(state);
                return true;
            }

            if (worldItems == null
                || !worldItems.SpawnUniqueItemAt(
                    order.packageItemId,
                    order.SourcePosition,
                    WorldItemStackState.Loose,
                    order.destinationId,
                    order.DestinationPosition,
                    out string packageStackId))
            {
                order.phase = FacilityRelocationPhase.Blocked;
                component.ReplaceInstanceEvolution(state);
                failureReason = "시설 포장물을 생성하지 못했습니다.";
                return false;
            }

            if (!relocationWorld.TryPackAtDestination(
                    facility,
                    order.DestinationPosition,
                    out failureReason))
            {
                worldItems.DeleteStack(packageStackId);
                order.phase = FacilityRelocationPhase.Blocked;
                component.ReplaceInstanceEvolution(state);
                return false;
            }

            order.packageStackId = packageStackId;
            order.phase = FacilityRelocationPhase.WaitingForPackage;
            component.ReplaceInstanceEvolution(state);
            facilityCandidateCache?.MarkDynamicStateDirty();
            return true;
        }

        if (order.phase == FacilityRelocationPhase.WaitingForPackage)
        {
            failureReason = "포장된 시설이 목적지에 도착하지 않았습니다.";
            return false;
        }

        if (order.phase != FacilityRelocationPhase.Reinstalling
            || !order.packageConsumed)
        {
            failureReason = "시설 이전 작업이 막혀 있습니다.";
            return false;
        }

        order.reinstallCompletedWork = Mathf.Clamp(
            order.reinstallCompletedWork + Mathf.Max(0f, workUnits),
            0f,
            order.reinstallRequiredWork);
        if (order.reinstallCompletedWork + 0.001f
            < order.reinstallRequiredWork)
        {
            component.ReplaceInstanceEvolution(state);
            return true;
        }

        state.relocationOrder = null;
        state.roomStructureVersion = -1;
        state.facilityStateVersion = -1;
        component.ReplaceInstanceEvolution(state);
        if (!relocationWorld.TryCompleteRelocation(
                facility,
                out relocatedFacility,
                out failureReason))
        {
            state.relocationOrder = order;
            order.phase = FacilityRelocationPhase.Blocked;
            component.ReplaceInstanceEvolution(state);
            return false;
        }

        RefreshRoomActivation(relocatedFacility);
        facilityCandidateCache?.MarkDynamicStateDirty();
        completed = true;
        return true;
    }

    public bool CancelPendingWork(
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        string destinationId = state.modificationOrder?.destinationId
            ?? state.recalibrationOrder?.destinationId
            ?? string.Empty;
        if (state.relocationOrder != null)
        {
            if (state.relocationOrder.phase
                != FacilityRelocationPhase.Dismantling)
            {
                failureReason =
                    "해체가 끝난 시설은 목적지에서 재설치해야 합니다.";
                return false;
            }

            state.relocationOrder = null;
            component.ReplaceInstanceEvolution(state);
            facilityCandidateCache?.MarkDynamicStateDirty();
            return true;
        }

        if (string.IsNullOrWhiteSpace(destinationId))
        {
            failureReason = "취소할 시설 개조 작업이 없습니다.";
            return false;
        }

        ReleaseDestination(destinationId, facility.centerPos);
        state.modificationOrder = null;
        state.recalibrationOrder = null;
        component.ReplaceInstanceEvolution(state);
        facilityCandidateCache?.MarkDynamicStateDirty();
        return true;
    }

    private bool TryApplyCandidateNow(
        BuildableObject facility,
        FacilityEvolutionStateComponent component,
        FacilityEvolutionState state,
        FacilityGenerationCandidate candidate,
        out EvolutionNode appliedNode,
        out string failureReason)
    {
        appliedNode = null;
        failureReason = string.Empty;
        if (candidate == null
            || !string.Equals(
                candidate.historyHash,
                state.pendingHistoryHash,
                StringComparison.Ordinal))
        {
            failureReason = "고정된 시설 기록과 현재 상태가 일치하지 않습니다.";
            return false;
        }

        if (!moduleRegistry.TryGet(
                candidate.benefitModuleId,
                out EvolutionModuleDefinition module))
        {
            failureReason =
                $"등록되지 않은 진화 효과입니다: {candidate.benefitModuleId}";
            return false;
        }

        string parentNodeId = state.evolutionNodes
            .Where(node => node != null && !node.historical)
            .OrderByDescending(node => node.generation)
            .ThenBy(node => node.nodeId, StringComparer.Ordinal)
            .Select(node => node.nodeId)
            .FirstOrDefault() ?? string.Empty;
        string nodeHash = StableEvolutionHash.Compute(
            state.facilityPersistentId + "|" + candidate.candidateId);
        EvolutionNode node = new EvolutionNode
        {
            nodeId = $"facility-node:{nodeHash}",
            parentNodeId = parentNodeId,
            effectId = module.ModuleId,
            burdenEffectId = candidate.burdenModuleId,
            generation = candidate.targetGeneration,
            active = true,
            historical = false,
            displayName = module.DisplayName,
            description = string.Empty,
            evidenceIds = state.usageLedger.compactedSegments
                .OrderByDescending(segment => segment.lastGeneration)
                .ThenByDescending(segment => segment.level)
                .SelectMany(segment => segment.keyEvents)
                .Where(entry => entry != null)
                .Take(8)
                .Select(entry => entry.evidenceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            activationRule = candidate.activationRule?.Clone()
                ?? new EvolutionModuleActivationRule()
        };
        state.evolutionNodes.Add(node);
        float spentMastery = FacilityEvolutionProgression.GetRequiredMastery(
            state.generation);
        state.mastery = Mathf.Max(0f, state.mastery - spentMastery);
        state.generation = candidate.targetGeneration;
        state.pendingCandidates.Clear();
        state.pendingHistoryHash = string.Empty;
        component.ReplaceInstanceEvolution(state);
        RefreshRoomActivation(facility);
        facilityCandidateCache?.MarkDynamicStateDirty();
        appliedNode = node.Clone();
        return true;
    }

    private bool RequestMaterials(
        Vector2Int position,
        string destinationId,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason)
    {
        failureReason = string.Empty;
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
                ReleaseDestination(destinationId, position);
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? $"개조 재료가 부족합니다: {requirement.Key}"
                    : requestFailure;
                return false;
            }
        }

        return true;
    }

    private bool EnsureMaterialsReady(
        FacilityModificationOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order.materialsConsumed
            && string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            return true;
        }

        if (!FacilityModificationMaterialOutbox.TryCommitOrFinalize(
                order,
                worldItems,
                batchDispositions,
                out failureReason))
        {
            return false;
        }

        return true;
    }

    private bool EnsureMaterialsReady(
        FacilityRecalibrationOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order.materialsConsumed
            && string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            return true;
        }

        if (!FacilityRecalibrationMaterialOutbox.TryCommitOrFinalize(
                order,worldItems,batchDispositions,out failureReason))
        {
            return false;
        }

        return true;
    }

    private void ReleaseDestination(
        string destinationId,
        Vector2Int position)
    {
        worldItems?.ReleaseStacksByDestination(destinationId, position);
    }

    public bool RefreshRoomActivation(BuildableObject facility)
    {
        FacilityEvolutionStateComponent component = RequireComponent(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        int gridVersion = facility.Grid?.StructuralVersion ?? -1;
        int facilityVersion = facilityCandidateCache?.DynamicStateVersion ?? -1;
        if (state.roomStructureVersion == gridVersion
            && state.facilityStateVersion == facilityVersion)
        {
            return false;
        }

        EvolutionRoomConditionSnapshot room = BuildRoomSnapshot(facility);
        List<string> active = new List<string>();
        List<string> dormant = new List<string>();
        foreach (EvolutionNode node in state.evolutionNodes
                     .Where(node => node != null && !node.historical))
        {
            if (EvolutionModuleActivation.IsBenefitActive(
                    node.activationRule,
                    room))
            {
                active.Add(node.nodeId);
            }
            else
            {
                dormant.Add(node.nodeId);
            }
        }

        state.activeNodeIds = active;
        state.dormantNodeIds = dormant;
        state.roomStructureVersion = gridVersion;
        state.facilityStateVersion = facilityVersion;
        component.ReplaceInstanceEvolution(state);
        return true;
    }

    private void EnsureCandidates(
        BuildableObject facility,
        FacilityEvolutionStateComponent component,
        FacilityEvolutionState state)
    {
        if (!state.ReadyForGeneration || state.pendingCandidates.Count > 0)
        {
            return;
        }

        CompactedHistorySegment closed = ledgerCompactor.CloseGeneration(
            state.usageLedger,
            state.generation);
        string historyHash = closed.historyHash;
        string seedKey = string.Join(
            "|",
            ResolveRunSeed().ToString(),
            state.facilityPersistentId,
            state.generation.ToString(),
            historyHash);
        IRandomStream random = new DeterministicRandomSequence(
            StableEvolutionHash.ToSeed(seedKey));
        string primaryModuleId =
            FacilityEvolutionRules.ResolvePrimaryModuleId(closed);
        EvolutionModuleActivationRule roomRule = BuildCurrentRoomRule(facility);
        int nextGeneration = state.generation + 1;
        int catalystProgressionLevel =
            EquipmentEvolutionProgression.GetMinimumCatalystProgressionLevel(
                state.generation);
        string catalystFamily = CatalystFamilies[
            random.NextInt(0, CatalystFamilies.Length)];

        state.pendingHistoryHash = historyHash;
        state.pendingCandidates = new List<FacilityGenerationCandidate>
        {
            CreateCandidate(
                state,
                historyHash,
                FacilityGenerationCandidateKind.PrimaryRole,
                nextGeneration,
                primaryModuleId,
                primaryModuleId,
                string.Empty,
                0,
                new EvolutionModuleActivationRule()),
            CreateCandidate(
                state,
                historyHash,
                FacilityGenerationCandidateKind.RoomSynergy,
                nextGeneration,
                "facility:room-synergy",
                "facility:room-synergy",
                string.Empty,
                0,
                roomRule),
            CreateCandidate(
                state,
                historyHash,
                FacilityGenerationCandidateKind.RiskyCatalyst,
                nextGeneration,
                "facility:risky-overdrive",
                "facility:risky-overdrive",
                catalystFamily,
                catalystProgressionLevel,
                new EvolutionModuleActivationRule())
        };
        component.ReplaceInstanceEvolution(state);
    }

    private void EnsureNarrativeSnapshot(FacilityEvolutionState state)
    {
        if (state == null
            || state.RequiredMastery <= 0f
            || state.mastery / state.RequiredMastery < 0.9f)
        {
            return;
        }

        int targetGeneration = state.generation + 1;
        state.narrativeRequests ??=
            new List<EvolutionNarrativeRequestSnapshot>();
        if (state.narrativeRequests.Any(request =>
                request != null
                && request.targetKind == EvolutionNarrativeTargetKind.Facility
                && request.generation == targetGeneration))
        {
            return;
        }

        string historyHash = ledgerCompactor.ComputeHistoryHash(
            state.usageLedger);
        string parentNodeId = state.evolutionNodes
            .Where(node => node != null && node.historical)
            .OrderByDescending(node => node.generation)
            .ThenBy(node => node.nodeId, StringComparer.Ordinal)
            .Select(node => node.nodeId)
            .FirstOrDefault() ?? string.Empty;
        string historyNodeHash = StableEvolutionHash.Compute(
            state.facilityPersistentId
            + "|"
            + targetGeneration
            + "|"
            + historyHash);
        EvolutionNode historyNode = new EvolutionNode
        {
            nodeId = $"facility-history:{historyNodeHash}",
            parentNodeId = parentNodeId,
            effectId = "history:facility",
            burdenEffectId = string.Empty,
            generation = targetGeneration,
            active = true,
            historical = true,
            playerVisible = false,
            displayName = string.Empty,
            description = string.Empty,
            potencyMultiplier = 1f,
            activationRule = new EvolutionModuleActivationRule()
        };
        EvolutionNarrativeRequestSnapshot request =
            EvolutionNarrativeRequestFactory.Create(
                EvolutionNarrativeTargetKind.Facility,
                state.facilityPersistentId,
                historyNode,
                historyHash,
                state.usageLedger,
                effectBudget: 0);
        historyNode.evidenceIds = new List<string>(request.evidenceIds);
        state.evolutionNodes.Add(historyNode);
        state.narrativeRequests.Add(request);
    }

    private FacilityGenerationCandidate CreateCandidate(
        FacilityEvolutionState state,
        string historyHash,
        FacilityGenerationCandidateKind kind,
        int generation,
        string benefitModuleId,
        string burdenModuleId,
        string catalystFamily,
        int minimumCatalystProgressionLevel,
        EvolutionModuleActivationRule activationRule)
    {
        string candidateHash = StableEvolutionHash.Compute(string.Join(
            "|",
            ResolveRunSeed().ToString(),
            state.facilityPersistentId,
            generation.ToString(),
            historyHash,
            kind.ToString(),
            benefitModuleId,
            burdenModuleId,
            catalystFamily,
            minimumCatalystProgressionLevel.ToString()));
        return new FacilityGenerationCandidate
        {
            candidateId = $"facility-candidate:{candidateHash}",
            kind = kind,
            targetGeneration = generation,
            benefitModuleId = benefitModuleId,
            burdenModuleId = burdenModuleId,
            catalystFamily = catalystFamily,
            minimumCatalystProgressionLevel =
                minimumCatalystProgressionLevel,
            historyHash = historyHash,
            activationRule = activationRule?.Clone()
                ?? new EvolutionModuleActivationRule()
        };
    }

    private EvolutionModuleActivationRule BuildCurrentRoomRule(
        BuildableObject facility)
    {
        if (roomEnvironment == null
            || !roomEnvironment.TryGetSnapshot(
                facility,
                out RoomEnvironmentSnapshot snapshot)
            || snapshot == null
            || !snapshot.IsEnvironmentActive)
        {
            return new EvolutionModuleActivationRule
            {
                kind = EvolutionModuleActivationKind.RoomConditional,
                minimumCleanliness = 40f,
                minimumSpace = 35f
            };
        }

        string primaryTag = snapshot.PrimaryRole != FacilityRole.None
            ? snapshot.PrimaryRole.ToString()
            : string.Empty;
        return new EvolutionModuleActivationRule
        {
            kind = EvolutionModuleActivationKind.RoomConditional,
            requiredRoomTags = string.IsNullOrWhiteSpace(primaryTag)
                ? new List<string>()
                : new List<string> { primaryTag },
            minimumCleanliness = Mathf.Floor(snapshot.Cleanliness * 0.8f),
            minimumBeauty = Mathf.Floor(snapshot.Beauty * 0.8f),
            minimumTemperature = Mathf.Floor(snapshot.Temperature * 0.8f),
            minimumSpace = Mathf.Floor(snapshot.Spaciousness * 0.8f)
        };
    }

    private EvolutionRoomConditionSnapshot BuildRoomSnapshot(
        BuildableObject facility)
    {
        if (roomEnvironment == null
            || !roomEnvironment.TryGetSnapshot(
                facility,
                out RoomEnvironmentSnapshot snapshot)
            || snapshot == null)
        {
            return new EvolutionRoomConditionSnapshot(
                Array.Empty<string>(),
                0f,
                0f,
                0f,
                0f);
        }

        List<string> tags = Enum.GetValues(typeof(FacilityRole))
            .Cast<FacilityRole>()
            .Where(role => role != FacilityRole.None
                && snapshot.Roles.HasFlag(role))
            .Select(role => role.ToString())
            .ToList();
        return new EvolutionRoomConditionSnapshot(
            tags,
            snapshot.Cleanliness,
            snapshot.Beauty,
            snapshot.Temperature,
            snapshot.Spaciousness);
    }

    private int ResolveRunSeed()
    {
        return runSeedProvider.RunSeed;
    }

    private FacilityEvolutionStateComponent RequireComponent(
        BuildableObject facility)
    {
        if (facility == null || facility.isDestroy)
        {
            throw new ArgumentException(
                "A live facility is required.",
                nameof(facility));
        }

        return stateFactory.GetOrAdd(facility)
            ?? throw new InvalidOperationException(
                "Facility evolution state component could not be created.");
    }

}
