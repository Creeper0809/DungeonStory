using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum KnowledgeResidueUse
{
    CodexAnalysis = 0,
    RegionReconnaissance = 1
}

[Serializable]
public sealed class KnowledgeResidueTaskSaveData
{
    public string taskId = string.Empty;
    public KnowledgeResidueUse use;
    public string regionId = string.Empty;
    public float requiredWork = 24f;
    public float completedWork;
    public int facilityId;
    public int facilityX;
    public int facilityY;
    public string destinationId = string.Empty;
}

public readonly struct KnowledgeResidueTaskSnapshot
{
    public KnowledgeResidueTaskSnapshot(KnowledgeResidueTaskSaveData source)
    {
        TaskId = source?.taskId ?? string.Empty;
        Use = source?.use ?? KnowledgeResidueUse.CodexAnalysis;
        RegionId = source?.regionId ?? string.Empty;
        RequiredWork = Mathf.Max(1f, source?.requiredWork ?? 1f);
        CompletedWork = Mathf.Clamp(
            source?.completedWork ?? 0f,
            0f,
            RequiredWork);
        FacilityId = source?.facilityId ?? 0;
        FacilityPosition = new Vector2Int(
            source?.facilityX ?? 0,
            source?.facilityY ?? 0);
        DestinationId = source?.destinationId ?? string.Empty;
    }

    public string TaskId { get; }
    public KnowledgeResidueUse Use { get; }
    public string RegionId { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public float ProgressRatio => Mathf.Clamp01(CompletedWork / RequiredWork);
    public int FacilityId { get; }
    public Vector2Int FacilityPosition { get; }
    public string DestinationId { get; }
}

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
    IReadOnlyList<KnowledgeResidueTaskSaveData> Capture();
    void Restore(
        IEnumerable<KnowledgeResidueTaskSaveData> tasks,
        DungeonGameRestoreReport report);
}

public sealed class KnowledgeResidueProcessingRuntime :
    IKnowledgeResidueProcessingRuntime,
    ITickable
{
    private const float DefaultRequiredWork = 24f;
    private const float DeliveryCheckInterval = 0.5f;

    private readonly IWorldItemStackRuntime items;
    private readonly IBuildingWorldQuery buildings;
    private readonly ICodexRuntimeProvider codexProvider;
    private readonly IOffenseRegionRuntime regions;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameEventBus eventBus;
    private readonly IGameClock gameClock;
    private readonly List<KnowledgeResidueTaskSaveData> tasks =
        new List<KnowledgeResidueTaskSaveData>();
    private int nextTaskSequence = 1;
    private float nextDeliveryCheckAt;
    private string readySignaledTaskId = string.Empty;

    public KnowledgeResidueProcessingRuntime(
        IWorldItemStackRuntime items,
        IBuildingWorldQuery buildings,
        ICodexRuntimeProvider codexProvider,
        IOffenseRegionRuntime regions,
        IWorkforceReplanService workforce,
        IGameEventBus eventBus,
        IGameClock gameClock)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.codexProvider = codexProvider
            ?? throw new ArgumentNullException(nameof(codexProvider));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public IReadOnlyList<KnowledgeResidueTaskSnapshot> Tasks =>
        tasks.Select(task => new KnowledgeResidueTaskSnapshot(task)).ToArray();

    public bool TryQueueCodexAnalysis(out string message)
    {
        if (!codexProvider.TryGetRuntime(out CodexRuntime codex))
        {
            message = "도감 시스템을 불러오지 못했습니다.";
            return false;
        }

        if (!codex.HasMemoryResidueClueAvailable)
        {
            message = "분석 가능한 기억 잔재 단서를 모두 정리했습니다.";
            return false;
        }

        if (tasks.Any(task => task.use == KnowledgeResidueUse.CodexAnalysis))
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

        if (tasks.Any(task =>
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
        if (gameClock.IsPaused
            || tasks.Count == 0
            || gameClock.Time < nextDeliveryCheckAt)
        {
            return;
        }

        nextDeliveryCheckAt = gameClock.Time + DeliveryCheckInterval;
        KnowledgeResidueTaskSaveData task = tasks[0];
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
            if (!string.Equals(
                    readySignaledTaskId,
                    task.taskId,
                    StringComparison.Ordinal))
            {
                readySignaledTaskId = task.taskId;
                workforce.RequestOneWorkerToReplanFor(
                    BuiltInWorkTypeIds.Research,
                    forceInterrupt: true);
            }

            return;
        }

        readySignaledTaskId = string.Empty;
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
        if (facility == null
            || tasks.Count == 0
            || !facility.SupportsWork(BuiltInWorkTypeIds.Research))
        {
            return false;
        }

        KnowledgeResidueTaskSaveData task = tasks[0];
        return IsAssignedFacility(task, facility) && HasDeliveredKnowledge(task);
    }

    public BlueprintResearchWorkResult ApplyWork(
        CharacterActor researcher,
        BuildableObject facility,
        float seconds)
    {
        if (!HasProcessingWorkFor(facility))
        {
            return Failure("기억 잔재가 아직 연구 시설에 도착하지 않았습니다.");
        }

        KnowledgeResidueTaskSaveData task = tasks[0];
        float added = DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantWork)
            ? task.requiredWork
            : BlueprintResearchService.CalculateResearchWork(
                researcher,
                facility,
                seconds);
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
            tasks.RemoveAt(0);
            readySignaledTaskId = string.Empty;
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

        tasks.RemoveAt(0);
        readySignaledTaskId = string.Empty;
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
        return tasks.Select(Clone).ToArray();
    }

    public void Restore(
        IEnumerable<KnowledgeResidueTaskSaveData> savedTasks,
        DungeonGameRestoreReport report)
    {
        tasks.Clear();
        readySignaledTaskId = string.Empty;
        nextTaskSequence = 1;
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (KnowledgeResidueTaskSaveData saved in
                 savedTasks ?? Array.Empty<KnowledgeResidueTaskSaveData>())
        {
            if (saved == null
                || string.IsNullOrWhiteSpace(saved.taskId)
                || !ids.Add(saved.taskId))
            {
                report?.AddWarning("중복되거나 잘못된 기억 잔재 작업을 건너뜁니다.");
                continue;
            }

            KnowledgeResidueTaskSaveData restored = Clone(saved);
            restored.requiredWork = Mathf.Max(1f, restored.requiredWork);
            restored.completedWork = Mathf.Clamp(
                restored.completedWork,
                0f,
                restored.requiredWork);
            restored.destinationId = string.IsNullOrWhiteSpace(restored.destinationId)
                ? $"knowledge:{restored.taskId}"
                : restored.destinationId;
            tasks.Add(restored);
            nextTaskSequence = Mathf.Max(
                nextTaskSequence,
                ParseSequence(restored.taskId) + 1);
        }
    }

    private void Queue(KnowledgeResidueUse use, string regionId)
    {
        string taskId = $"knowledge-{nextTaskSequence++:D5}";
        tasks.Add(new KnowledgeResidueTaskSaveData
        {
            taskId = taskId,
            use = use,
            regionId = regionId ?? string.Empty,
            requiredWork = DefaultRequiredWork,
            destinationId = $"knowledge:{taskId}"
        });
        nextDeliveryCheckAt = 0f;
        readySignaledTaskId = string.Empty;
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
                if (!codexProvider.TryGetRuntime(out CodexRuntime codex))
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
            if (!codexProvider.TryGetRuntime(out CodexRuntime codex))
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
