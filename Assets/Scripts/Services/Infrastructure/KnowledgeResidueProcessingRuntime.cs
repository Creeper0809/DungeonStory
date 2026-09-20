using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public interface IKnowledgeResidueProcessingRuntime
{
    IReadOnlyList<KnowledgeResidueTaskSnapshot> Tasks { get; }
    int NextTaskSequence { get; }
    int TaskIdentityGeneration { get; }
    bool TryQueueCodexAnalysis(out string message);
    bool TryQueueRegionReconnaissance(string regionId, out string message);
    bool HasProcessingWorkFor(BuildableObject facility);
    BlueprintResearchWorkResult ApplyWork(
        CharacterActor researcher,
        BuildableObject facility,
        float seconds);
    BlueprintResearchWorkResult ApplyApprovedWork(
        CharacterActor researcher,
        BuildableObject facility,
        float approvedWorkUnits,
        DurableFacilityEquipmentUseContext equipment = null);
    IReadOnlyList<KnowledgeResidueTaskSaveData> Capture();
    KnowledgeResidueRestoreCandidate PrepareRestore(
        IEnumerable<KnowledgeResidueTaskSaveData> tasks,
        int nextTaskSequence = 0,
        int taskIdentityGeneration = 0);
    void Restore(KnowledgeResidueRestoreCandidate candidate);
}

public sealed class KnowledgeResidueExecutionServices
{
    public KnowledgeResidueExecutionServices(
        IWorkforceReplanService workforce,
        IGameEventBus eventBus,
        IGameClock gameClock,
        IDungeonDebugRuleQuery debugRules)
    {
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        DebugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
    }

    public IWorkforceReplanService Workforce { get; }
    public IGameEventBus EventBus { get; }
    public IGameClock GameClock { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
}

public sealed class KnowledgeResidueProcessingRuntime :
    IKnowledgeResidueProcessingRuntime,
    ITickable
{
    private const float DefaultRequiredWork = 24f;
    private const float DeliveryCheckInterval = 0.5f;

    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalFacilityItemSinkGateway physicalSinks;
    private readonly IKnowledgeResidueDestinationRuntime destinations;
    private readonly IFacilityBufferDestinationReleaseService releases;
    private readonly IBuildingWorldQuery buildings;
    private readonly CodexRuntime codex;
    private readonly IOffenseRegionRuntime regions;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameEventBus eventBus;
    private readonly IGameClock gameClock;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private BlueprintResearchOutcomeTransaction gameplayOutcomes;
    private KnowledgeResidueCompletionTransaction completionOutcomes;

    [Inject]
    public void ConstructCompletionOutcomes(KnowledgeResidueCompletionTransaction outcomes) =>
        completionOutcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));

    [Inject]
    public void ConstructGameplayOutcomes(BlueprintResearchOutcomeTransaction outcomes) =>
        gameplayOutcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));

    public KnowledgeResidueProcessingRuntime(
        IWorldItemStackRuntime items,
        IPhysicalFacilityItemSinkGateway physicalSinks,
        IKnowledgeResidueDestinationRuntime destinations,
        IFacilityBufferDestinationReleaseService releases,
        IBuildingWorldQuery buildings,
        FacilityFeatureSceneRuntimeReferences facilityRuntimes,
        IOffenseRegionRuntime regions,
        KnowledgeResidueExecutionServices executionServices,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.physicalSinks = physicalSinks
            ?? throw new ArgumentNullException(nameof(physicalSinks));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        codex = (facilityRuntimes
                ?? throw new ArgumentNullException(nameof(facilityRuntimes)))
            .Codex
            ?? throw new InvalidOperationException(
                $"{nameof(KnowledgeResidueProcessingRuntime)} requires a loaded {nameof(CodexRuntime)}.");
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        KnowledgeResidueExecutionServices execution = executionServices
            ?? throw new ArgumentNullException(nameof(executionServices));
        workforce = execution.Workforce;
        eventBus = execution.EventBus;
        gameClock = execution.GameClock;
        debugRules = execution.DebugRules;
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyList<KnowledgeResidueTaskSnapshot> Tasks =>
        CurrentState.Tasks
            .Select(task => new KnowledgeResidueTaskSnapshot(task))
            .ToArray();
    public int NextTaskSequence => CurrentState.CaptureNextTaskSequence;
    public int TaskIdentityGeneration =>
        CurrentState.CaptureTaskIdentityGeneration;

    private KnowledgeResidueAggregateState CurrentState =>
        aggregateRootStore.GetOrCreate(
            () => new KnowledgeResidueAggregateState());

    private KnowledgeResidueAggregateState WritableState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new KnowledgeResidueAggregateState(),
            state => state.DeepClone());

    public bool TryQueueCodexAnalysis(out string message)
    {
        if (!CanAllocateTask(out message)) return false;
        if (codex == null)
        {
            message = "도감 시스템을 불러오지 못했습니다.";
            return false;
        }

        if (!codex.HasMemoryResidueClueAvailable)
        {
            message = "분석 가능한 기억 잔재 단서를 모두 정리했습니다.";
            return false;
        }

        if (CurrentState.Tasks.Any(task =>
                task.use == KnowledgeResidueUse.CodexAnalysis))
        {
            message = "이미 기억 잔재 도감 분석이 대기 중입니다.";
            return false;
        }

        Queue(KnowledgeResidueUse.CodexAnalysis, string.Empty);
        message = "기억 잔재를 연구 시설로 운반해 도감 단서를 분석합니다.";
        return true;
    }

    public bool TryQueueRegionReconnaissance(string regionId, out string message)
    {
        if (!CanAllocateTask(out message)) return false;
        string normalizedRegionId = regionId?.Trim() ?? string.Empty;
        if (normalizedRegionId.Length == 0
            || regions.Regions.FirstOrDefault(region => string.Equals(
                region?.regionId,
                normalizedRegionId,
                StringComparison.Ordinal)) is not OffenseRegionState targetRegion)
        {
            message = "정찰할 지역을 찾을 수 없습니다.";
            return false;
        }

        if (targetRegion.intelligenceDamage >= 99.999f)
        {
            message = $"{targetRegion.displayName}의 정보망은 이미 완전히 무력화되었습니다.";
            return false;
        }

        if (CurrentState.Tasks.Any(task =>
                task.use == KnowledgeResidueUse.RegionReconnaissance
                && string.Equals(
                    task.regionId,
                    normalizedRegionId,
                    StringComparison.Ordinal)))
        {
            message = "이 지역의 기억 잔재 정찰이 이미 대기 중입니다.";
            return false;
        }

        Queue(KnowledgeResidueUse.RegionReconnaissance, normalizedRegionId);
        message = $"기억 잔재를 분석해 {targetRegion.displayName} 정찰을 준비합니다.";
        return true;
    }

    private bool CanAllocateTask(out string message)
    {
        message = CurrentState.CanAllocateTaskSequence ? string.Empty
            : "기억 잔재 작업 번호를 안전하게 발급할 수 없습니다. 구형 저장의 작업 기록 확인 또는 번호 범위 복구가 필요합니다.";
        return CurrentState.CanAllocateTaskSequence;
    }

    public void Tick()
    {
        KnowledgeResidueAggregateState current = CurrentState;
        if (gameClock.IsPaused
            || !current.HasTasks
            || !current.IsDeliveryCheckDue(gameClock.Time))
        {
            return;
        }

        KnowledgeResidueAggregateState state = WritableState;
        state.ScheduleNextDeliveryCheck(gameClock.Time + DeliveryCheckInterval);
        KnowledgeResidueTaskSaveData task = state.FirstTask;
        if (task.dispositionPhase !=
                KnowledgeResidueDispositionPhase.AwaitingInput
            || task.completedWork + .001f >= task.requiredWork
                && physicalSinks.TryGetPending(task.sinkOperationId, out _))
        {
            if (!TryFinalizeCommittedTask(
                    state,
                    task,
                    out _,
                    out string finalizeFailure))
            {
                ReportFinalizationPending(finalizeFailure);
            }
            return;
        }
        BuildableObject facility = ResolveAssignedFacility(task);
        if (facility == null)
        {
            facility = FindResearchFacility();
            if (facility == null)
            {
                return;
            }

            if (!destinations.TryEnsure(
                    task,
                    facility,
                    out string authorityFailure))
            {
                throw new InvalidOperationException(
                    "Knowledge residue destination publication failed: "
                    + authorityFailure);
            }
        }

        if (HasDeliveredKnowledge(task))
        {
            if (task.completedWork + .001f >= task.requiredWork)
            {
                if (!TryFinalizeCommittedTask(state, task, out _, out string failure))
                    ReportFinalizationPending(failure);
                return;
            }
            if (state.SetReadySignal(task.taskId))
            {
                workforce.RequestOneWorkerToReplanFor(
                    BuiltInWorkTypeIds.Research,
                    forceInterrupt: true);
            }

            return;
        }

        state.ClearReadySignal();
        if (HasOutstandingDelivery(task))
        {
            return;
        }

        if (items.TryRequestItemDelivery(
                KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                1,
                facility.centerPos,
                task.destinationId,
                out int requested,
                out _)
            && requested > 0)
        {
            foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
            {
                if (stack != null
                    && string.Equals(
                        stack.DestinationId,
                        task.destinationId,
                        StringComparison.Ordinal))
                {
                    items.PrioritizeHaul(stack.StackId);
                }
            }

            workforce.RequestOneHaulerToReplan(forceInterrupt: true);
        }
    }

    public bool HasProcessingWorkFor(BuildableObject facility)
    {
        KnowledgeResidueAggregateState state = CurrentState;
        if (facility == null
            || !state.HasTasks
            || !facility.SupportsWork(BuiltInWorkTypeIds.Research))
        {
            return false;
        }

        KnowledgeResidueTaskSaveData task = state.FirstTask;
        return IsAssignedFacility(task, facility)
            && task.dispositionPhase == KnowledgeResidueDispositionPhase.AwaitingInput
            && task.completedWork + .001f < task.requiredWork
            && HasDeliveredKnowledge(task);
    }

    public BlueprintResearchWorkResult ApplyWork(
        CharacterActor researcher,
        BuildableObject facility,
        float seconds) =>
        ApplyWorkInternal(
            researcher,
            facility,
            seconds,
            approvedWorkUnits: false);

    public BlueprintResearchWorkResult ApplyApprovedWork(
        CharacterActor researcher,
        BuildableObject facility,
        float approvedWorkUnits,
        DurableFacilityEquipmentUseContext equipment = null) =>
        ApplyWorkInternal(
            researcher,
            facility,
            approvedWorkUnits,
            approvedWorkUnits: true,
            equipment);

    private BlueprintResearchWorkResult ApplyWorkInternal(
        CharacterActor researcher,
        BuildableObject facility,
        float amount,
        bool approvedWorkUnits,
        DurableFacilityEquipmentUseContext equipment = null)
    {
        if (!HasProcessingWorkFor(facility))
        {
            return Failure("기억 잔재가 아직 연구 시설에 도착하지 않았습니다.");
        }

        if (gameplayOutcomes == null)
            throw new InvalidOperationException("Knowledge research requires its gameplay-outcome transaction.");
        KnowledgeResidueTaskSaveData task = CurrentState.FirstTask;
        float added = debugRules.IsEnabled(DungeonDebugCheat.InstantWork)
            ? task.requiredWork
            : approvedWorkUnits
                ? BlueprintResearchService.CalculateApprovedResearchWork(
                    researcher,
                    amount)
                : BlueprintResearchService.CalculateResearchWork(
                    researcher,
                    facility,
                    amount);
        string researcherId = string.Empty;
        if (researcher != null)
        {
            if (!CharacterPersistentIdentity.TryGet(researcher, out CharacterId id))
                throw new InvalidOperationException("Knowledge researcher requires a persistent identity.");
            researcherId = id.Value;
        }
        if (!gameplayOutcomes.TryApplyKnowledgeProgress(aggregateRootStore, added,
                Mathf.Max(0, Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay)),
                GetTaskLabel(task), researcherId, researcher?.Identity.DisplayName,
                facility.RequirePersistentInstanceId().Value,
                FacilityShopService.GetBuildingName(facility.BuildingData),
                equipment == null ? null : new ResearchEquipmentOutcomeEvidence(equipment),
                out BlueprintResearchWorkResult progress))
            return progress;
        if (progress.TotalProgress + .001f < progress.RequiredWork)
            return progress;

        // Progress and the exact equipment use are already committed. Re-query the
        // detached owner replacement; finalization must never turn them into failed work.
        KnowledgeResidueAggregateState state = WritableState;
        task = state.FirstTask;
        bool finalized = TryFinalizeCommittedTask(state, task, out bool completed, out string resultMessage);
        if (!finalized) ReportFinalizationPending(resultMessage);
        return new BlueprintResearchWorkResult(
            true,
            null,
            progress.AddedProgress,
            progress.TotalProgress,
            progress.RequiredWork,
            finalized && completed,
            progress.Message + " · " + (finalized ? resultMessage : "결과 처리 대기: " + resultMessage));
    }

    private bool TryCommitOrResumeInput(
        KnowledgeResidueTaskSaveData task,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (task == null)
        {
            failureReason = "knowledge-residue-task-missing";
            return false;
        }

        if (task.dispositionPhase ==
            KnowledgeResidueDispositionPhase.AwaitingInput)
        {
            if (completionOutcomes == null || physicalSinks is not IOutcomeAwarePhysicalFacilityItemSinkGateway joint)
            { failureReason = "knowledge-joint-completion-unavailable"; return false; }
            var participant = completionOutcomes.Prepare(aggregateRootStore, task, codex, regions,
                Math.Max(0, (int)(gameClock.Time / GameCalendarRules.SecondsPerDay)),
                FacilityShopService.GetBuildingName(ResolveAssignedFacility(task)?.BuildingData));
            if (!joint.TryCommitSinkPending(
                    task.destinationId,
                    KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                    1,
                    task.sinkOperationId,
                    task.sinkReasonCode,
                    participant,
                    out receipt,
                    out failureReason))
            {
                return false;
            }
            try
            {
                participant.NotifyCommitted();
                if (task.use == KnowledgeResidueUse.RegionReconnaissance)
                    eventBus.RaiseAlert("기억 정찰 완료", GetPublishedResultMessage(CurrentState.FirstTask),
                        EventAlertImportance.Medium, "오펜스");
            }
            catch (Exception exception) when (IsRecoverable(exception))
            { ReportFinalizationPending("knowledge-completion-observer:" + exception.GetType().Name); }
            return true;
        }

        if (!physicalSinks.TryGetPending(task.sinkOperationId, out receipt))
        {
            failureReason = "knowledge-residue-pending-sink-missing:"
                + task.sinkOperationId;
            return false;
        }
        if (!ReceiptMatches(task, receipt))
        {
            receipt = default;
            failureReason = "knowledge-residue-pending-sink-mismatch:"
                + task.sinkOperationId;
            return false;
        }
        if (completionOutcomes == null || !completionOutcomes.IsCommitted(task, receipt))
        { failureReason = "knowledge-completion-canonical-proof-missing"; return false; }
        return true;
    }

    private bool TryFinalizeCommittedTask(
        KnowledgeResidueAggregateState state,
        KnowledgeResidueTaskSaveData task,
        out bool completed,
        out string failureReason)
    {
        completed = false;
        try
        {
            return TryFinalizeCommittedTaskCore(state, task, out completed, out failureReason);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failureReason = "knowledge-finalization-exception:" + exception.GetType().Name;
            return false;
        }
    }

    private bool TryFinalizeCommittedTaskCore(
        KnowledgeResidueAggregateState state,
        KnowledgeResidueTaskSaveData task,
        out bool completed,
        out string failureReason)
    {
        completed = false;
        failureReason = string.Empty;
        if (state == null || task == null)
        {
            failureReason = "knowledge-residue-committed-task-missing";
            return false;
        }
        // Old split-commit saves require explicit reconciliation; do not apply
        // an uncertain legacy reward again or consume a second input.
        if (task.dispositionPhase == KnowledgeResidueDispositionPhase.AwaitingInput
            && physicalSinks.TryGetPending(task.sinkOperationId, out _)
            || task.dispositionPhase != KnowledgeResidueDispositionPhase.AwaitingInput
                && task.completionOwnerRevision <= 0)
        { failureReason = "knowledge-legacy-completion-reconciliation-required"; return false; }
        if (!CanApplyResult(task, out string invalidReason))
        {
            BuildableObject facility = ResolveAssignedFacility(task);
            if (facility == null)
            {
                failureReason = "knowledge-invalid-task-facility-missing";
                return false;
            }
            if (!releases.TryReleaseAtOwnerPosition(task.destinationId, facility.centerPos,
                    "knowledge-residue-task-invalid", out _, out failureReason)
                || !destinations.TryRevoke(task, out failureReason))
                return false;
            state.RemoveFirstTask();
            state.ClearReadySignal();
            NotifyFinalized();
            failureReason = invalidReason + " 기억 잔재는 시설 앞에 돌려놓았습니다.";
            return true;
        }
        if (!TryCommitOrResumeInput(
                task,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }
        state = WritableState;
        task = state.FirstTask;
        if (task.dispositionPhase !=
            KnowledgeResidueDispositionPhase.OutcomePublished)
        {
            failureReason = "knowledge-residue-outcome-not-published";
            return false;
        }
        string resultMessage = GetPublishedResultMessage(task);
        if (!destinations.TryRevoke(task, out failureReason))
        {
            return false;
        }
        if (physicalSinks is not IOutcomeAwarePhysicalFacilityItemSinkGateway cleanupGateway)
        { failureReason = "knowledge-joint-cleanup-unavailable"; return false; }
        if (!cleanupGateway.Acknowledge(
                receipt.CommitId,
                KnowledgeResidueCompletionTransaction.PrepareCleanup(aggregateRootStore, task),
                out failureReason))
        {
            return false;
        }
        completed = true;
        failureReason = resultMessage;
        NotifyFinalized();
        return true;
    }

    private void NotifyFinalized()
    {
        try { workforce.RequestIdleWorkersToReplan(); }
        catch (Exception exception) when (IsRecoverable(exception))
        { ReportFinalizationPending("knowledge-post-commit-replan:" + exception.GetType().Name); }
    }

    private static void ReportFinalizationPending(string reason)
    {
        try { Debug.LogWarning("Knowledge residue committed work requires finalization: " + reason); }
        catch (Exception exception) when (IsRecoverable(exception)) { }
    }

    private static bool IsRecoverable(Exception exception) => exception is not
        OutOfMemoryException and not StackOverflowException and not AccessViolationException;

    internal static void StoreCommittedReceipt(
        KnowledgeResidueTaskSaveData task,
        PhysicalItemBatchDispositionReceipt receipt)
    {
        if (!receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.OperationId,
                task.sinkOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                task.sinkReasonCode,
                StringComparison.Ordinal)
            || receipt.Quantity != 1
            || receipt.InputMassGrams != task.inputCapacityGrams)
        {
            throw new InvalidOperationException(
                "Knowledge residue sink receipt does not match its task authority.");
        }
        task.sinkSourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        task.sinkRequestFingerprint = receipt.RequestFingerprint;
        task.sinkInputMassGrams = receipt.InputMassGrams;
        task.sinkCommitId = receipt.CommitId;
        task.dispositionPhase =
            KnowledgeResidueDispositionPhase.InputCommitted;
    }

    private static bool ReceiptMatches(
        KnowledgeResidueTaskSaveData task,
        PhysicalItemBatchDispositionReceipt receipt) =>
        task != null
        && receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(receipt.OperationId, task.sinkOperationId,
            StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, task.sinkReasonCode,
            StringComparison.Ordinal)
        && string.Equals(receipt.RequestFingerprint,
            task.sinkRequestFingerprint,
            StringComparison.Ordinal)
        && receipt.Quantity == 1
        && receipt.InputMassGrams == task.inputCapacityGrams
        && receipt.InputMassGrams == task.sinkInputMassGrams
        && string.Equals(receipt.CommitId, task.sinkCommitId,
            StringComparison.Ordinal)
        && receipt.SourceStackIds.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(
                (task.sinkSourceStackIds ?? new List<string>())
                    .OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);

    public IReadOnlyList<KnowledgeResidueTaskSaveData> Capture()
    {
        return CurrentState.Tasks.Select(Clone).ToArray();
    }

    public KnowledgeResidueRestoreCandidate PrepareRestore(
        IEnumerable<KnowledgeResidueTaskSaveData> savedTasks,
        int nextTaskSequence = 0,
        int taskIdentityGeneration = 0)
    {
        if (savedTasks == null)
        {
            throw new InvalidOperationException(
                "Knowledge residue task collection is missing.");
        }

        KnowledgeResidueAggregateState restored =
            new KnowledgeResidueAggregateState();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (KnowledgeResidueTaskSaveData saved in savedTasks)
        {
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Knowledge residue task collection contains null.");
            }
            RequireCanonicalId(saved.taskId, "knowledge task");
            if (!KnowledgeResidueTaskIdentity.TryParse(
                    saved.taskId,
                    out _,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task id '{saved.taskId}' is not in canonical sequence format.");
            }
            if (!ids.Add(saved.taskId))
            {
                throw new InvalidOperationException(
                    $"Duplicate knowledge residue task id '{saved.taskId}'.");
            }
            if (!Enum.IsDefined(typeof(KnowledgeResidueUse), saved.use))
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task '{saved.taskId}' has invalid use {saved.use}.");
            }
            if (!float.IsFinite(saved.requiredWork) || saved.requiredWork < 1f)
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task '{saved.taskId}' has invalid required work.");
            }
            if (!float.IsFinite(saved.completedWork)
                || saved.completedWork < 0f
                || saved.completedWork > saved.requiredWork)
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task '{saved.taskId}' has invalid completed work.");
            }
            if (saved.facilityId < 0)
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task '{saved.taskId}' has invalid facility id.");
            }
            if (!Enum.IsDefined(
                    typeof(KnowledgeResidueDispositionPhase),
                    saved.dispositionPhase))
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task '{saved.taskId}' has invalid disposition phase.");
            }
            if (!string.Equals(
                    saved.destinationId,
                    KnowledgeResidueDestinationAuthority.FormatDestinationId(
                        saved.taskId,
                        saved.assignmentSequence),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task '{saved.taskId}' has non-canonical destination id.");
            }
            ValidatePhysicalContract(saved);
            if (saved.completionOwnerRevision < 0
                || saved.completionOwnerRevision > 0
                    && saved.dispositionPhase != KnowledgeResidueDispositionPhase.OutcomePublished)
                throw new InvalidOperationException("Knowledge completion phase/identity is invalid: " + saved.taskId);
            if (saved.use == KnowledgeResidueUse.RegionReconnaissance)
            {
                RequireCanonicalId(saved.regionId, "knowledge region");
            }
            else if (!string.IsNullOrEmpty(saved.regionId))
            {
                throw new InvalidOperationException(
                    $"Codex knowledge task '{saved.taskId}' cannot target a region.");
            }

            KnowledgeResidueTaskSaveData restoredTask = Clone(saved);
            restored.AddRestoredTask(restoredTask);
        }

        restored.RestoreAllocationIdentity(
            nextTaskSequence,
            taskIdentityGeneration);
        return new KnowledgeResidueRestoreCandidate(restored);
    }

    public void Restore(KnowledgeResidueRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .TakeStateForRestore());
    }

    private static void RequireCanonicalId(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} id must be non-empty and canonical.");
        }
    }

    private static void ValidatePhysicalContract(
        KnowledgeResidueTaskSaveData task)
    {
        if (task.assignmentSequence <= 0
            || !string.Equals(
                task.sinkOperationId,
                KnowledgeResidueDestinationAuthority.FormatSinkOperationId(
                    task.taskId),
                StringComparison.Ordinal)
            || !string.Equals(
                task.sinkReasonCode,
                KnowledgeResidueDestinationAuthority.SinkReasonCode,
                StringComparison.Ordinal)
            || task.sinkSourceStackIds == null
            || task.dispositionPhase !=
                KnowledgeResidueDispositionPhase.AwaitingInput
                && string.IsNullOrWhiteSpace(task.sinkRequestFingerprint)
            || task.sinkSourceStackIds.Any(value =>
                string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || task.sinkSourceStackIds.Distinct(StringComparer.Ordinal).Count()
                != task.sinkSourceStackIds.Count
            || !float.IsFinite(task.appliedReconnaissanceAmount)
            || task.appliedReconnaissanceAmount < 0f)
        {
            throw new InvalidOperationException(
                $"Knowledge residue task '{task.taskId}' has invalid physical identity.");
        }

        bool assigned = task.facilityId > 0;
        if (assigned != (!string.IsNullOrEmpty(task.facilityInstanceId)
                && task.inputCapacityGrams > 0L
                && task.massAuthorityRevision > 0L
                && !string.IsNullOrEmpty(task.inputCapacityFingerprint)))
        {
            throw new InvalidOperationException(
                $"Knowledge residue task '{task.taskId}' has a partial facility projection.");
        }
        if (!assigned
            && (task.facilityX != 0
                || task.facilityY != 0
                || task.dispositionPhase !=
                    KnowledgeResidueDispositionPhase.AwaitingInput))
        {
            throw new InvalidOperationException(
                $"Unassigned knowledge residue task '{task.taskId}' has physical progress.");
        }

        bool committed = task.dispositionPhase !=
            KnowledgeResidueDispositionPhase.AwaitingInput;
        if (committed != (!string.IsNullOrEmpty(task.sinkRequestFingerprint)
                && task.sinkSourceStackIds.Count > 0
                && task.sinkInputMassGrams > 0L
                && !string.IsNullOrEmpty(task.sinkCommitId)))
        {
            throw new InvalidOperationException(
                $"Knowledge residue task '{task.taskId}' has a partial Sink receipt.");
        }
        if (committed && task.sinkInputMassGrams != task.inputCapacityGrams)
        {
            throw new InvalidOperationException(
                $"Knowledge residue task '{task.taskId}' Sink mass does not match its input profile.");
        }

        if (task.use == KnowledgeResidueUse.CodexAnalysis)
        {
            RequireCanonicalId(task.codexCluePayload, "knowledge codex clue");
            if (task.appliedReconnaissanceAmount != 0f)
            {
                throw new InvalidOperationException(
                    $"Codex knowledge task '{task.taskId}' cannot contain reconnaissance output.");
            }
        }
        else if (!string.IsNullOrEmpty(task.codexCluePayload)
                 || task.dispositionPhase !=
                     KnowledgeResidueDispositionPhase.OutcomePublished
                    && task.appliedReconnaissanceAmount != 0f
                 || task.dispositionPhase ==
                     KnowledgeResidueDispositionPhase.OutcomePublished
                    && task.appliedReconnaissanceAmount <= 0f)
        {
            throw new InvalidOperationException(
                $"Reconnaissance knowledge task '{task.taskId}' has invalid result payload.");
        }
    }

    private void Queue(KnowledgeResidueUse use, string regionId)
    {
        KnowledgeResidueAggregateState state = WritableState;
        string taskId = state.AllocateTaskId();
        const int initialAssignmentSequence = 1;
        string codexClue = string.Empty;
        if (use == KnowledgeResidueUse.CodexAnalysis
            && !codex.TryGetNextMemoryResidueClue(out codexClue))
        {
            throw new InvalidOperationException(
                "Knowledge residue codex task has no deterministic clue payload.");
        }
        state.AddTask(new KnowledgeResidueTaskSaveData
        {
            taskId = taskId,
            use = use,
            regionId = regionId ?? string.Empty,
            requiredWork = DefaultRequiredWork,
            assignmentSequence = initialAssignmentSequence,
            destinationId = KnowledgeResidueDestinationAuthority
                .FormatDestinationId(taskId, initialAssignmentSequence),
            sinkOperationId = KnowledgeResidueDestinationAuthority
                .FormatSinkOperationId(taskId),
            sinkReasonCode = KnowledgeResidueDestinationAuthority
                .SinkReasonCode,
            codexCluePayload = codexClue
        });
        state.ScheduleNextDeliveryCheck(0f);
        state.ClearReadySignal();
        eventBus.RaiseAlert(
            "기억 잔재 처리",
            use == KnowledgeResidueUse.CodexAnalysis
                ? "연구 시설에서 도감 단서를 분석할 준비를 시작합니다."
                : "연구 시설에서 지역 정찰 기억을 분석할 준비를 시작합니다.",
            EventAlertImportance.Low,
            "연구");
    }

    private bool CanApplyResult(
        KnowledgeResidueTaskSaveData task,
        out string message)
    {
        if (task.dispositionPhase !=
            KnowledgeResidueDispositionPhase.AwaitingInput)
        {
            message = string.Empty;
            return true;
        }
        if (task.use == KnowledgeResidueUse.CodexAnalysis)
        {
            if (codex == null)
            {
                message = "도감 시스템을 불러오지 못했습니다.";
                return false;
            }

            if (!codex.HasMemoryResidueClueAvailable)
            {
                message = "분석 가능한 도감 단서가 더 없습니다.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        if (task.use == KnowledgeResidueUse.RegionReconnaissance)
        {
            OffenseRegionState region = regions.Regions.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.regionId,
                    task.regionId,
                    StringComparison.Ordinal));
            if (region == null)
            {
                message = "정찰할 지역이 사라졌습니다.";
                return false;
            }

            if (region.intelligenceDamage >= 99.999f)
            {
                message = $"{region.displayName}의 정보망은 이미 완전히 무력화되었습니다.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        message = "지원하지 않는 기억 잔재 처리 방식입니다.";
        return false;
    }

    private BuildableObject FindResearchFacility()
    {
        return buildings.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && building.SupportsWork(BuiltInWorkTypeIds.Research))
            .OrderBy(building => building.id)
            .ThenBy(building => building.centerPos.x)
            .ThenBy(building => building.centerPos.y)
            .FirstOrDefault();
    }

    private BuildableObject ResolveAssignedFacility(
        KnowledgeResidueTaskSaveData task)
    {
        if (task == null || task.facilityId == 0)
        {
            return null;
        }

        BuildableObject facility = buildings.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && candidate.id == task.facilityId
            && candidate.centerPos.x == task.facilityX
            && candidate.centerPos.y == task.facilityY
            && string.Equals(
                candidate.RequirePersistentInstanceId().Value,
                task.facilityInstanceId,
                StringComparison.Ordinal)
            && candidate.SupportsWork(BuiltInWorkTypeIds.Research));
        if (facility != null)
        {
            return facility;
        }

        int nextAssignmentSequence = checked(task.assignmentSequence + 1);
        if (!releases.TryReleaseAtOwnerPosition(
                task.destinationId,
                new Vector2Int(task.facilityX, task.facilityY),
                "knowledge-residue-facility-lost",
                out _,
                out string releaseFailure))
        {
            throw new InvalidOperationException(
                "Knowledge residue lost-facility physical release failed: "
                + releaseFailure);
        }
        if (!destinations.TryRevoke(task, out string revokeFailure))
        {
            throw new InvalidOperationException(
                "Knowledge residue lost-facility authority revoke failed: "
                + revokeFailure);
        }
        task.facilityId = 0;
        task.facilityX = 0;
        task.facilityY = 0;
        task.facilityInstanceId = string.Empty;
        task.inputCapacityGrams = 0L;
        task.massAuthorityRevision = 0L;
        task.inputCapacityFingerprint = string.Empty;
        task.assignmentSequence = nextAssignmentSequence;
        task.destinationId = KnowledgeResidueDestinationAuthority
            .FormatDestinationId(task.taskId, task.assignmentSequence);
        return null;
    }

    private static bool IsAssignedFacility(
        KnowledgeResidueTaskSaveData task,
        BuildableObject facility)
    {
        return task != null
            && facility != null
            && task.facilityId == facility.id
            && task.facilityX == facility.centerPos.x
            && task.facilityY == facility.centerPos.y
            && string.Equals(
                task.facilityInstanceId,
                facility.RequirePersistentInstanceId().Value,
                StringComparison.Ordinal);
    }

    private bool HasDeliveredKnowledge(KnowledgeResidueTaskSaveData task)
    {
        return items.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && string.Equals(
                stack.ItemId,
                KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                StringComparison.Ordinal)
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.DestinationId,
                task.destinationId,
                StringComparison.Ordinal));
    }

    private bool HasOutstandingDelivery(KnowledgeResidueTaskSaveData task)
    {
        return items.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && string.Equals(
                stack.ItemId,
                KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.DestinationId,
                task.destinationId,
                StringComparison.Ordinal));
    }

    private static BlueprintResearchWorkResult Failure(string message)
    {
        return new BlueprintResearchWorkResult(
            false,
            null,
            0f,
            0f,
            1f,
            false,
            message);
    }

    private static string GetTaskLabel(KnowledgeResidueTaskSaveData task)
    {
        return task.use == KnowledgeResidueUse.CodexAnalysis
            ? "도감 단서 분석"
            : "지역 기억 정찰";
    }

    private string GetPublishedResultMessage(
        KnowledgeResidueTaskSaveData task)
    {
        if (task.use == KnowledgeResidueUse.CodexAnalysis)
        {
            return $"월간 단서 정보: {task.codexCluePayload}";
        }
        OffenseRegionState region = regions.Regions.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.regionId,
                task.regionId,
                StringComparison.Ordinal));
        return region == null
            ? $"{task.regionId} 정보망 약화 +{task.appliedReconnaissanceAmount:0.#}"
            : $"{region.displayName} 정보망 약화 +{task.appliedReconnaissanceAmount:0.#}";
    }

    private static KnowledgeResidueTaskSaveData Clone(
        KnowledgeResidueTaskSaveData source)
    {
        return new KnowledgeResidueTaskSaveData
        {
            taskId = source?.taskId ?? string.Empty,
            use = source?.use ?? KnowledgeResidueUse.CodexAnalysis,
            regionId = source?.regionId ?? string.Empty,
            requiredWork = source?.requiredWork ?? DefaultRequiredWork,
            completedWork = source?.completedWork ?? 0f,
            facilityId = source?.facilityId ?? 0,
            facilityX = source?.facilityX ?? 0,
            facilityY = source?.facilityY ?? 0,
            assignmentSequence = source?.assignmentSequence ?? 0,
            destinationId = source?.destinationId ?? string.Empty,
            facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
            inputCapacityGrams = source?.inputCapacityGrams ?? 0L,
            massAuthorityRevision = source?.massAuthorityRevision ?? 0L,
            inputCapacityFingerprint = source?.inputCapacityFingerprint
                ?? string.Empty,
            dispositionPhase = source?.dispositionPhase
                ?? KnowledgeResidueDispositionPhase.AwaitingInput,
            sinkOperationId = source?.sinkOperationId ?? string.Empty,
            sinkReasonCode = source?.sinkReasonCode ?? string.Empty,
            sinkRequestFingerprint = source?.sinkRequestFingerprint
                ?? string.Empty,
            sinkSourceStackIds = new List<string>(
                source?.sinkSourceStackIds ?? new List<string>()),
            sinkInputMassGrams = source?.sinkInputMassGrams ?? 0L,
            sinkCommitId = source?.sinkCommitId ?? string.Empty,
            codexCluePayload = source?.codexCluePayload ?? string.Empty,
            completionOwnerRevision = source?.completionOwnerRevision ?? 0,
            appliedReconnaissanceAmount =
                source?.appliedReconnaissanceAmount ?? 0f
        };
    }

}
