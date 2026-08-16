using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IKnowledgeResidueProcessingRuntime
{
    IReadOnlyList<KnowledgeResidueTaskSnapshot> Tasks { get; }
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
        float approvedWorkUnits);
    IReadOnlyList<KnowledgeResidueTaskSaveData> Capture();
    KnowledgeResidueRestoreCandidate PrepareRestore(
        IEnumerable<KnowledgeResidueTaskSaveData> tasks);
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
    private readonly IBuildingWorldQuery buildings;
    private readonly CodexRuntime codex;
    private readonly IOffenseRegionRuntime regions;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameEventBus eventBus;
    private readonly IGameClock gameClock;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    public KnowledgeResidueProcessingRuntime(
        IWorldItemStackRuntime items,
        IBuildingWorldQuery buildings,
        FacilityFeatureSceneRuntimeReferences facilityRuntimes,
        IOffenseRegionRuntime regions,
        KnowledgeResidueExecutionServices executionServices,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
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

    private KnowledgeResidueAggregateState CurrentState =>
        aggregateRootStore.GetOrCreate(
            () => new KnowledgeResidueAggregateState());

    private KnowledgeResidueAggregateState WritableState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new KnowledgeResidueAggregateState(),
            state => state.DeepClone());

    public bool TryQueueCodexAnalysis(out string message)
    {
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
        BuildableObject facility = ResolveAssignedFacility(task);
        if (facility == null)
        {
            facility = FindResearchFacility();
            if (facility == null)
            {
                return;
            }

            AssignFacility(task, facility);
        }

        if (HasDeliveredKnowledge(task))
        {
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

        if (items.TryRequestFacilityDelivery(
                StockCategory.Knowledge,
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
        return IsAssignedFacility(task, facility) && HasDeliveredKnowledge(task);
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
        float approvedWorkUnits) =>
        ApplyWorkInternal(
            researcher,
            facility,
            approvedWorkUnits,
            approvedWorkUnits: true);

    private BlueprintResearchWorkResult ApplyWorkInternal(
        CharacterActor researcher,
        BuildableObject facility,
        float amount,
        bool approvedWorkUnits)
    {
        if (!HasProcessingWorkFor(facility))
        {
            return Failure("기억 잔재가 아직 연구 시설에 도착하지 않았습니다.");
        }

        KnowledgeResidueAggregateState state = WritableState;
        KnowledgeResidueTaskSaveData task = state.FirstTask;
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
        float before = task.completedWork;
        task.completedWork = Mathf.Clamp(
            task.completedWork + Mathf.Max(0f, added),
            0f,
            Mathf.Max(1f, task.requiredWork));
        added = task.completedWork - before;
        if (task.completedWork + 0.001f < task.requiredWork)
        {
            return new BlueprintResearchWorkResult(
                true,
                null,
                added,
                task.completedWork,
                task.requiredWork,
                false,
                $"{GetTaskLabel(task)} {Mathf.RoundToInt(task.completedWork / task.requiredWork * 100f)}%");
        }

        if (!CanApplyResult(task, out string invalidReason))
        {
            items.ReleaseStacksByDestination(
                task.destinationId,
                facility.centerPos);
            state.RemoveFirstTask();
            state.ClearReadySignal();
            workforce.RequestIdleWorkersToReplan();
            return Failure($"{invalidReason} 기억 잔재는 시설 앞에 돌려놓았습니다.");
        }

        Dictionary<StockCategory, int> cost = new Dictionary<StockCategory, int>
        {
            [StockCategory.Knowledge] = 1
        };
        if (!items.TryConsumeFacilityBuffer(
                task.destinationId,
                cost,
                out string consumeFailure))
        {
            task.completedWork = Mathf.Max(0f, task.requiredWork - 0.01f);
            return Failure($"기억 잔재 소비 실패: {consumeFailure}");
        }

        bool applied = ApplyResult(task, out string resultMessage);
        if (!applied)
        {
            return Failure(resultMessage);
        }

        state.RemoveFirstTask();
        state.ClearReadySignal();
        workforce.RequestIdleWorkersToReplan();
        return new BlueprintResearchWorkResult(
            true,
            null,
            added,
            task.requiredWork,
            task.requiredWork,
            true,
            resultMessage);
    }

    public IReadOnlyList<KnowledgeResidueTaskSaveData> Capture()
    {
        return CurrentState.Tasks.Select(Clone).ToArray();
    }

    public KnowledgeResidueRestoreCandidate PrepareRestore(
        IEnumerable<KnowledgeResidueTaskSaveData> savedTasks)
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
            int taskSequence = ParseSequence(saved.taskId);
            if (taskSequence < 1
                || !string.Equals(
                    saved.taskId,
                    $"knowledge-{taskSequence:D5}",
                    StringComparison.Ordinal))
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
            if (!string.Equals(
                    saved.destinationId,
                    $"knowledge:{saved.taskId}",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Knowledge residue task '{saved.taskId}' has non-canonical destination id.");
            }
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
            restored.AddRestoredTask(restoredTask, taskSequence);
        }

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

    private void Queue(KnowledgeResidueUse use, string regionId)
    {
        KnowledgeResidueAggregateState state = WritableState;
        string taskId = $"knowledge-{state.AllocateTaskSequence():D5}";
        state.AddTask(new KnowledgeResidueTaskSaveData
        {
            taskId = taskId,
            use = use,
            regionId = regionId ?? string.Empty,
            requiredWork = DefaultRequiredWork,
            destinationId = $"knowledge:{taskId}"
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

    private bool ApplyResult(
        KnowledgeResidueTaskSaveData task,
        out string message)
    {
        switch (task.use)
        {
            case KnowledgeResidueUse.CodexAnalysis:
                if (codex == null)
                {
                    message = "도감 시스템을 불러오지 못했습니다.";
                    return false;
                }

                return codex.TryRecordMemoryResidueClue(out message);

            case KnowledgeResidueUse.RegionReconnaissance:
                if (!regions.TryApplyReconnaissance(
                        task.regionId,
                        10f,
                        out float applied))
                {
                    message = "지역 정찰 결과를 적용하지 못했습니다.";
                    return false;
                }

                OffenseRegionState region = regions.Regions.First(candidate =>
                    string.Equals(
                        candidate.regionId,
                        task.regionId,
                        StringComparison.Ordinal));
                message = $"{region.displayName} 정보망 약화 +{applied:0.#}";
                eventBus.RaiseAlert(
                    "기억 정찰 완료",
                    message,
                    EventAlertImportance.Medium,
                    "오펜스");
                return true;

            default:
                message = "지원하지 않는 기억 잔재 처리 방식입니다.";
                return false;
        }
    }

    private bool CanApplyResult(
        KnowledgeResidueTaskSaveData task,
        out string message)
    {
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
            && candidate.SupportsWork(BuiltInWorkTypeIds.Research));
        if (facility != null)
        {
            return facility;
        }

        items.ReleaseStacksByDestination(
            task.destinationId,
            new Vector2Int(task.facilityX, task.facilityY));
        task.facilityId = 0;
        task.facilityX = 0;
        task.facilityY = 0;
        return null;
    }

    private static void AssignFacility(
        KnowledgeResidueTaskSaveData task,
        BuildableObject facility)
    {
        task.facilityId = facility.id;
        task.facilityX = facility.centerPos.x;
        task.facilityY = facility.centerPos.y;
    }

    private static bool IsAssignedFacility(
        KnowledgeResidueTaskSaveData task,
        BuildableObject facility)
    {
        return task != null
            && facility != null
            && task.facilityId == facility.id
            && task.facilityX == facility.centerPos.x
            && task.facilityY == facility.centerPos.y;
    }

    private bool HasDeliveredKnowledge(KnowledgeResidueTaskSaveData task)
    {
        return items.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && stack.StockCategory == StockCategory.Knowledge
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
            && stack.StockCategory == StockCategory.Knowledge
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
            destinationId = source?.destinationId ?? string.Empty
        };
    }

    private static int ParseSequence(string taskId)
    {
        string value = taskId?.Split('-').LastOrDefault() ?? string.Empty;
        return int.TryParse(value, out int sequence) ? sequence : 0;
    }
}
