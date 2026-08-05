using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

[Serializable]
public sealed class EvolutionHistoryNarrativeResponseDto : ILlmJsonPayload
{
    public string requestKey = string.Empty;
    public string targetPersistentId = string.Empty;
    public string nodeId = string.Empty;
    public string parentNodeId = string.Empty;
    public string effectId = string.Empty;
    public int effectBudget;
    public string[] evidenceIds = Array.Empty<string>();
    public string displayName = string.Empty;
    public string description = string.Empty;
    public string historyReason = string.Empty;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(requestKey)
            || string.IsNullOrWhiteSpace(targetPersistentId)
            || string.IsNullOrWhiteSpace(nodeId)
            || string.IsNullOrWhiteSpace(effectId))
        {
            error = "Evolution history identifiers are required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName)
            || displayName.Trim().Length > 32)
        {
            error = "displayName must contain 1-32 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(description)
            || description.Trim().Length > 180)
        {
            error = "description must contain 1-180 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(historyReason)
            || historyReason.Trim().Length > 180)
        {
            error = "historyReason must contain 1-180 characters.";
            return false;
        }

        return true;
    }
}

public interface IEvolutionHistoryNarrativeRuntime
{
    int PendingCount { get; }
    bool TryApplyResponseForDebug(
        string requestKey,
        string response,
        out string failureReason);
    void CancelTarget(
        EvolutionNarrativeTargetKind targetKind,
        string targetPersistentId);
}

public static class EvolutionNarrativeResponseValidator
{
    public static bool Validate(
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (request == null
            || payload == null
            || !payload.Validate(out failureReason)
            || !string.Equals(payload.requestKey, request.requestKey, StringComparison.Ordinal)
            || !string.Equals(
                payload.targetPersistentId,
                request.targetPersistentId,
                StringComparison.Ordinal)
            || !string.Equals(payload.nodeId, request.nodeId, StringComparison.Ordinal)
            || !string.Equals(
                payload.parentNodeId ?? string.Empty,
                request.parentNodeId ?? string.Empty,
                StringComparison.Ordinal)
            || !string.Equals(payload.effectId, request.effectId, StringComparison.Ordinal)
            || payload.effectBudget != request.effectBudget)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason =
                    "Evolution narrative identifiers or effect budget changed.";
            }
            return false;
        }

        HashSet<string> expectedEvidence = new HashSet<string>(
            request.evidenceIds ?? new List<string>(),
            StringComparer.Ordinal);
        HashSet<string> actualEvidence = new HashSet<string>(
            payload.evidenceIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        if (!expectedEvidence.SetEquals(actualEvidence))
        {
            failureReason =
                "Evolution narrative evidence does not match the locked snapshot.";
            return false;
        }

        return true;
    }
}

public static class EvolutionNarrativeRequestFactory
{
    public static EvolutionNarrativeRequestSnapshot Create(
        EvolutionNarrativeTargetKind targetKind,
        string targetPersistentId,
        EvolutionNode node,
        string historyHash,
        UsageLedger ledger,
        int effectBudget)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        List<UsageLedgerEvent> evidence = (ledger?.currentGenerationEvents
                ?? new List<UsageLedgerEvent>())
            .Where(entry => entry != null)
            .OrderByDescending(entry => Mathf.Abs(entry.amount))
            .ThenBy(entry => entry.sequence)
            .ThenBy(entry => entry.evidenceId, StringComparer.Ordinal)
            .Take(8)
            .Select(entry => entry.Clone())
            .ToList();
        string normalizedTarget = targetPersistentId?.Trim() ?? string.Empty;
        string requestKey = $"evolution-history:{targetKind}:{normalizedTarget}:{node.nodeId}:{historyHash}";
        return new EvolutionNarrativeRequestSnapshot
        {
            requestKey = requestKey,
            targetKind = targetKind,
            targetPersistentId = normalizedTarget,
            nodeId = node.nodeId ?? string.Empty,
            parentNodeId = node.parentNodeId ?? string.Empty,
            effectId = node.effectId ?? string.Empty,
            historyHash = historyHash ?? string.Empty,
            generation = Mathf.Max(0, node.generation),
            effectBudget = Mathf.Max(0, effectBudget),
            evidenceIds = evidence
                .Select(entry => entry.evidenceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            participantIds = evidence
                .SelectMany(entry => new[] { entry.actorId, entry.targetId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            sourceTags = evidence
                .SelectMany(entry => entry.sourceTags ?? new List<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList()
        };
    }
}

public sealed class EvolutionHistoryNarrativeRuntime :
    IEvolutionHistoryNarrativeRuntime,
    ITickable
{
    private const float ScanIntervalSeconds = 0.25f;
    private const int MaximumConcurrentRequests = 2;
    private const int MaximumSubmissionsPerTick = 1;

    private readonly IBuildingWorldQuery buildings;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IFacilityEvolutionStateComponentFactory facilityStates;
    private readonly ILocalLlmRuntimeProvider llmRuntimeProvider;
    private readonly IUiClock uiClock;
    private readonly HashSet<string> inFlight =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> retryAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly HashSet<string> liveKeys =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<EvolutionNarrativeRequestSnapshot> scanBuffer =
        new List<EvolutionNarrativeRequestSnapshot>();
    private float nextScanAt;

    public EvolutionHistoryNarrativeRuntime(
        IBuildingWorldQuery buildings,
        ICombatEquipmentRuntime equipment,
        IFacilityEvolutionStateComponentFactory facilityStates,
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        IUiClock uiClock)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.facilityStates = facilityStates
            ?? throw new ArgumentNullException(nameof(facilityStates));
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public int PendingCount => retryAt.Count + inFlight.Count;

    public void Tick()
    {
        float now = uiClock.Time;
        if (now < nextScanAt)
        {
            return;
        }

        nextScanAt = now + ScanIntervalSeconds;
        CollectPendingSnapshots();
        CancelOrphanedRequests();

        int submissions = 0;
        for (int index = 0;
             index < scanBuffer.Count
             && inFlight.Count < MaximumConcurrentRequests
             && submissions < MaximumSubmissionsPerTick;
             index++)
        {
            EvolutionNarrativeRequestSnapshot request = scanBuffer[index];
            if (request == null
                || request.completed
                || request.cancelled
                || inFlight.Contains(request.requestKey)
                || retryAt.TryGetValue(request.requestKey, out float due)
                    && now < due)
            {
                continue;
            }

            submissions++;
            TrySubmit(request, now);
        }
    }

    public bool TryApplyResponseForDebug(
        string requestKey,
        string response,
        out string failureReason)
    {
        return TryApplyResponse(
            requestKey,
            response,
            out failureReason);
    }

    public void CancelTarget(
        EvolutionNarrativeTargetKind targetKind,
        string targetPersistentId)
    {
        string normalized = targetPersistentId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return;
        }

        string[] keys = liveKeys
            .Concat(inFlight)
            .Concat(retryAt.Keys)
            .Where(key => TryResolveSnapshot(
                key,
                out EvolutionNarrativeRequestSnapshot request)
                && request.targetKind == targetKind
                && string.Equals(
                    request.targetPersistentId,
                    normalized,
                    StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string key in keys)
        {
            CancelRequest(key);
        }
    }

    private void CollectPendingSnapshots()
    {
        scanBuffer.Clear();
        liveKeys.Clear();
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null || building.isDestroy)
            {
                continue;
            }

            FacilityEvolutionStateComponent component =
                building.GetComponent<FacilityEvolutionStateComponent>();
            if (component == null)
            {
                continue;
            }

            component.InitializeIfNeeded(building);
            FacilityEvolutionState state = component.InstanceEvolution;
            AddPending(state.narrativeRequests);
        }

        foreach (CombatEquipmentInstance instance in equipment.Instances)
        {
            AddPending(instance?.evolution?.narrativeRequests);
        }
    }

    private void AddPending(
        IEnumerable<EvolutionNarrativeRequestSnapshot> requests)
    {
        foreach (EvolutionNarrativeRequestSnapshot request in requests
                     ?? Array.Empty<EvolutionNarrativeRequestSnapshot>())
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.requestKey))
            {
                continue;
            }

            liveKeys.Add(request.requestKey);
            if (!request.completed && !request.cancelled)
            {
                scanBuffer.Add(request.Clone());
            }
        }
    }

    private void CancelOrphanedRequests()
    {
        string[] orphaned = inFlight
            .Concat(retryAt.Keys)
            .Where(key => !liveKeys.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string requestKey in orphaned)
        {
            CancelRequest(requestKey);
        }
    }

    private void TrySubmit(
        EvolutionNarrativeRequestSnapshot request,
        float now)
    {
        if (!TryResolveSnapshot(
                request.requestKey,
                out EvolutionNarrativeRequestSnapshot current)
            || current.completed
            || current.cancelled)
        {
            return;
        }

        if (!llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            ScheduleRetry(current, now);
            return;
        }

        current.attemptCount++;
        PersistRequest(current);
        string prompt = BuildPrompt(current);
        inFlight.Add(current.requestKey);
        bool accepted = runtime is ICorrelatedEvolutionHistoryLlmRuntime correlated
            ? correlated.GenerateEvolutionHistoryAsync(
                current.requestKey,
                prompt,
                result => HandleResult(current.requestKey, result))
            : runtime.GenerateFacilityEvolutionAsync(
                prompt,
                result => HandleResult(current.requestKey, result));
        if (!accepted)
        {
            inFlight.Remove(current.requestKey);
            ScheduleRetry(current, now);
        }
    }

    private void HandleResult(string requestKey, LocalLlmResult result)
    {
        inFlight.Remove(requestKey);
        if (!TryResolveSnapshot(
                requestKey,
                out EvolutionNarrativeRequestSnapshot current)
            || current.cancelled
            || current.completed)
        {
            return;
        }

        if (result.IsSuccess
            && TryApplyResponse(
                requestKey,
                result.Content,
                out _))
        {
            retryAt.Remove(requestKey);
            return;
        }

        ScheduleRetry(current, uiClock.Time);
    }

    private bool TryApplyResponse(
        string requestKey,
        string response,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryResolveSnapshot(
                requestKey,
                out EvolutionNarrativeRequestSnapshot request)
            || request.cancelled
            || request.completed)
        {
            failureReason = "Evolution narrative target is no longer available.";
            return false;
        }

        if (!LlmJsonResponseParser.TryParse(
                response,
                out EvolutionHistoryNarrativeResponseDto payload,
                out failureReason)
            || !EvolutionNarrativeResponseValidator.Validate(
                request,
                payload,
                out failureReason))
        {
            return false;
        }

        bool updated = request.targetKind == EvolutionNarrativeTargetKind.Facility
            ? ApplyFacilityNarrative(request, payload)
            : ApplyEquipmentNarrative(request, payload);
        if (!updated)
        {
            failureReason = "Evolution narrative target changed before the response arrived.";
            return false;
        }

        return true;
    }

    private bool ApplyFacilityNarrative(
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        BuildableObject facility = FindFacility(request.targetPersistentId);
        if (facility == null)
        {
            return false;
        }

        FacilityEvolutionStateComponent component =
            facilityStates.GetOrAdd(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        if (!TryApplyToState(state, request, payload))
        {
            return false;
        }

        component.ReplaceInstanceEvolution(state);
        return true;
    }

    private bool ApplyEquipmentNarrative(
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        if (!equipment.TryGetInstance(
                request.targetPersistentId,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (!TryApplyToState(state, request, payload))
        {
            return false;
        }

        return equipment.TryUpdateEvolutionState(instance.instanceId, state);
    }

    private static bool TryApplyToState(
        FacilityEvolutionState state,
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        EvolutionNode node = state.evolutionNodes?.FirstOrDefault(entry =>
            MatchesNode(entry, request));
        EvolutionNarrativeRequestSnapshot stored =
            state.narrativeRequests?.FirstOrDefault(entry =>
                entry != null
                && string.Equals(
                    entry.requestKey,
                    request.requestKey,
                    StringComparison.Ordinal));
        return ApplyValidatedNarrative(node, stored, payload);
    }

    private static bool TryApplyToState(
        EquipmentEvolutionState state,
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        EvolutionNode node = state.evolutionNodes?.FirstOrDefault(entry =>
            MatchesNode(entry, request));
        EvolutionNarrativeRequestSnapshot stored =
            state.narrativeRequests?.FirstOrDefault(entry =>
                entry != null
                && string.Equals(
                    entry.requestKey,
                    request.requestKey,
                    StringComparison.Ordinal));
        return ApplyValidatedNarrative(node, stored, payload);
    }

    private static bool ApplyValidatedNarrative(
        EvolutionNode node,
        EvolutionNarrativeRequestSnapshot stored,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        if (node == null || stored == null || stored.completed || stored.cancelled)
        {
            return false;
        }

        node.displayName = payload.displayName.Trim();
        node.description = string.Join(
            "\n",
            payload.description.Trim(),
            payload.historyReason.Trim());
        node.playerVisible = true;
        stored.completed = true;
        return true;
    }

    private static bool MatchesNode(
        EvolutionNode node,
        EvolutionNarrativeRequestSnapshot request)
    {
        return node != null
            && node.historical
            && string.Equals(node.nodeId, request.nodeId, StringComparison.Ordinal)
            && string.Equals(
                node.parentNodeId ?? string.Empty,
                request.parentNodeId ?? string.Empty,
                StringComparison.Ordinal)
            && string.Equals(node.effectId, request.effectId, StringComparison.Ordinal);
    }

    private bool TryResolveSnapshot(
        string requestKey,
        out EvolutionNarrativeRequestSnapshot request)
    {
        request = null;
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null || building.isDestroy)
            {
                continue;
            }

            FacilityEvolutionStateComponent component =
                building.GetComponent<FacilityEvolutionStateComponent>();
            EvolutionNarrativeRequestSnapshot found = component?
                .InstanceEvolution?
                .narrativeRequests?
                .FirstOrDefault(entry => entry != null
                    && string.Equals(
                        entry.requestKey,
                        requestKey,
                        StringComparison.Ordinal));
            if (found != null)
            {
                request = found.Clone();
                return true;
            }
        }

        foreach (CombatEquipmentInstance instance in equipment.Instances)
        {
            EvolutionNarrativeRequestSnapshot found = instance?
                .evolution?
                .narrativeRequests?
                .FirstOrDefault(entry => entry != null
                    && string.Equals(
                        entry.requestKey,
                        requestKey,
                        StringComparison.Ordinal));
            if (found != null)
            {
                request = found.Clone();
                return true;
            }
        }

        return false;
    }

    private void PersistRequest(EvolutionNarrativeRequestSnapshot request)
    {
        if (request.targetKind == EvolutionNarrativeTargetKind.Facility)
        {
            BuildableObject facility = FindFacility(request.targetPersistentId);
            FacilityEvolutionStateComponent component =
                facility != null ? facilityStates.GetOrAdd(facility) : null;
            if (component == null)
            {
                return;
            }

            FacilityEvolutionState state = component.InstanceEvolution;
            ReplaceRequest(state.narrativeRequests, request);
            component.ReplaceInstanceEvolution(state);
            return;
        }

        if (equipment.TryGetInstance(
                request.targetPersistentId,
                out CombatEquipmentInstance instance))
        {
            EquipmentEvolutionState state = instance.evolution?.Clone()
                ?? new EquipmentEvolutionState();
            ReplaceRequest(state.narrativeRequests, request);
            equipment.TryUpdateEvolutionState(instance.instanceId, state);
        }
    }

    private static void ReplaceRequest(
        IList<EvolutionNarrativeRequestSnapshot> requests,
        EvolutionNarrativeRequestSnapshot replacement)
    {
        if (requests == null || replacement == null)
        {
            return;
        }

        for (int index = 0; index < requests.Count; index++)
        {
            EvolutionNarrativeRequestSnapshot current = requests[index];
            if (current != null
                && string.Equals(
                    current.requestKey,
                    replacement.requestKey,
                    StringComparison.Ordinal))
            {
                requests[index] = replacement.Clone();
                return;
            }
        }
    }

    private void ScheduleRetry(
        EvolutionNarrativeRequestSnapshot request,
        float now)
    {
        float delay = Mathf.Min(
            120f,
            2f * Mathf.Pow(2f, Mathf.Min(6, request?.attemptCount ?? 0)));
        retryAt[request?.requestKey ?? string.Empty] = now + delay;
    }

    private void CancelRequest(string requestKey)
    {
        inFlight.Remove(requestKey);
        retryAt.Remove(requestKey);
        if (llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime)
            && runtime is ICorrelatedEvolutionHistoryLlmRuntime correlated)
        {
            correlated.CancelEvolutionHistoryRequest(requestKey);
        }
    }

    private BuildableObject FindFacility(string facilityPersistentId)
    {
        return buildings.Buildings.FirstOrDefault(building =>
        {
            if (building == null || building.isDestroy)
            {
                return false;
            }

            FacilityEvolutionStateComponent component =
                building.GetComponent<FacilityEvolutionStateComponent>();
            return component != null
                && string.Equals(
                    component.FacilityPersistentId,
                    facilityPersistentId,
                    StringComparison.Ordinal);
        });
    }

    private static string BuildPrompt(
        EvolutionNarrativeRequestSnapshot request)
    {
        StringBuilder builder = new StringBuilder(1400);
        builder.AppendLine("규칙 시스템이 effectId와 효과 수치를 이미 확정했다. LLM은 이름과 역사 문구만 작성하며 기계적 효과를 추가하거나 변경하지 않는다.");
        builder.AppendLine("계승 후 장비 형태가 바뀔 수 있으므로 검, 활, 총열, 날, 현, 방아쇠처럼 특정 형태를 전제하는 표현을 사용하지 않는다.");
        builder.AppendLine("위력, 관통, 정밀, 속도, 제압, 처형, 내구처럼 모든 동계열 장비에 통용되는 표현만 사용한다.");
        builder.AppendLine("당신은 다크 판타지 던전 경영 게임의 시설·장비 역사를 쓰는 서술자다.");
        builder.AppendLine("아래 구조화된 사실만 사용한다. 효과, 수치, 증거, 인물, 사건을 새로 만들지 않는다.");
        builder.AppendLine("기술 상태, LLM, 요청, 실패, 대기라는 표현을 절대 쓰지 않는다.");
        builder.AppendLine("짧고 읽기 쉬운 한국어 이름과 두 문장의 설명을 작성한다.");
        builder.AppendLine("다음 JSON 하나만 반환한다:");
        builder.AppendLine("{\"requestKey\":\"...\",\"targetPersistentId\":\"...\",\"nodeId\":\"...\",\"parentNodeId\":\"...\",\"effectId\":\"...\",\"effectBudget\":0,\"evidenceIds\":[\"...\"],\"displayName\":\"...\",\"description\":\"...\",\"historyReason\":\"...\"}");
        builder.Append("requestKey=").AppendLine(request.requestKey);
        builder.Append("targetPersistentId=").AppendLine(request.targetPersistentId);
        builder.Append("nodeId=").AppendLine(request.nodeId);
        builder.Append("parentNodeId=").AppendLine(request.parentNodeId);
        builder.Append("effectId=").AppendLine(request.effectId);
        builder.Append("effectBudget=").AppendLine(request.effectBudget.ToString());
        builder.Append("historyHash=").AppendLine(request.historyHash);
        builder.Append("generation=").AppendLine(request.generation.ToString());
        builder.Append("evidenceIds=").AppendLine(string.Join(",", request.evidenceIds));
        builder.Append("participantIds=").AppendLine(string.Join(",", request.participantIds));
        builder.Append("sourceTags=").AppendLine(string.Join(",", request.sourceTags));
        return builder.ToString();
    }
}
