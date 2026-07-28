using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[Serializable]
public sealed class CharacterAiMemoryEntry
{
    public string label = string.Empty;
    public CharacterAiIntentionType intention = CharacterAiIntentionType.None;
    public CharacterAiBranch branch = CharacterAiBranch.None;
    public int facilityId = -1;
    public int targetGridId = -1;
    public string workTypeId = string.Empty;
    public AIActionFailureKind failureKind = AIActionFailureKind.None;
    public int gridX;
    public int gridY;
    public float movementDistance;
    public float sentiment;
    public float time;
}

[DisallowMultipleComponent]
[DrawWithUnity]
public sealed class CharacterAiMemoryRuntime : MonoBehaviour
{
    private const int MaxEntries = 24;
    private const float RecentWindowSeconds = 60f;

    [SerializeField, ReadOnly] private CharacterActor actor;
    [SerializeField, ReadOnly] private List<CharacterAiMemoryEntry> recentEntries =
        new List<CharacterAiMemoryEntry>();
    private IReadOnlyList<CharacterAiMemoryEntry> recentEntriesView;
    private IGameClock gameClock;
    private float nextPruneAt;

    private float Now => gameClock != null ? gameClock.Time : 0f;

    public IReadOnlyList<CharacterAiMemoryEntry> RecentEntries =>
        recentEntriesView ??= ReadOnlyView.List(recentEntries);

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    [Inject]
    public void Construct(IGameClock gameClock)
    {
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
        recentEntries ??= new List<CharacterAiMemoryEntry>();
        recentEntriesView ??= ReadOnlyView.List(recentEntries);
        nextPruneAt = 0f;
        Prune();
    }

    public void RecordDecision(
        CharacterAiBranch branch,
        CharacterAiIntentionType intention,
        string label,
        float sentiment = 0f)
    {
        CharacterAiMemoryEntry entry = AcquireEntry();
        entry.branch = branch;
        entry.intention = intention;
        entry.label = string.IsNullOrWhiteSpace(label)
            ? CharacterAiUtilityText.GetBranchLabel(branch)
            : label;
        entry.sentiment = Mathf.Clamp(sentiment, -1f, 1f);
        entry.time = Now;
        AddEntry(entry);
    }

    public void RecordFacility(
        BuildableObject building,
        CharacterAiBranch branch,
        string label,
        float sentiment)
    {
        CharacterAiMemoryEntry entry = AcquireEntry();
        entry.branch = branch;
        entry.intention = CharacterAiUtilityText.GetIntention(branch);
        entry.facilityId = building != null ? building.id : -1;
        entry.targetGridId = building != null ? building.GridId : -1;
        if (TryGetBuildingGridPosition(building, out Vector2Int position))
        {
            entry.gridX = position.x;
            entry.gridY = position.y;
        }

        entry.label = label ?? string.Empty;
        entry.sentiment = Mathf.Clamp(sentiment, -1f, 1f);
        entry.time = Now;
        AddEntry(entry);
    }

    public void RecordWork(
        WorkTypeId workTypeId,
        BuildableObject building,
        bool success,
        string detail = "")
    {
        CharacterAiMemoryEntry entry = AcquireEntry();
        entry.branch = CharacterAiBranch.Work;
        entry.intention = workTypeId == BuiltInWorkTypeIds.Haul
            ? CharacterAiIntentionType.Logistics
            : workTypeId == BuiltInWorkTypeIds.Hunt
                ? CharacterAiIntentionType.Hunt
                : CharacterAiIntentionType.Work;
        entry.facilityId = building != null ? building.id : -1;
        entry.targetGridId = building != null ? building.GridId : -1;
        entry.workTypeId = workTypeId.ToString();
        if (TryGetBuildingGridPosition(building, out Vector2Int position))
        {
            entry.gridX = position.x;
            entry.gridY = position.y;
        }

        entry.label = string.IsNullOrWhiteSpace(detail)
            ? $"{WorkTaskCatalog.GetDisplayName(workTypeId)} {(success ? "완료" : "실패")}"
            : detail;
        entry.sentiment = success ? 0.2f : -0.3f;
        entry.time = Now;
        AddEntry(entry);
    }

    public void RecordFailure(
        AIActionFailureKind failureKind,
        string label,
        Vector2Int position)
    {
        CharacterAiMemoryEntry entry = AcquireEntry();
        entry.branch = CharacterAiBranch.InterruptCheck;
        entry.intention = CharacterAiIntentionType.None;
        entry.failureKind = failureKind;
        entry.gridX = position.x;
        entry.gridY = position.y;
        entry.label = string.IsNullOrWhiteSpace(label)
            ? "행동 실패"
            : label;
        entry.sentiment = -0.55f;
        entry.time = Now;
        AddEntry(entry);
    }

    public void RecordMovement(
        Vector2Int position,
        float distance,
        bool success,
        string label = "")
    {
        CharacterAiMemoryEntry entry = AcquireEntry();
        entry.branch = CharacterAiBranch.ContinueCurrent;
        entry.intention = CharacterAiIntentionType.None;
        entry.gridX = position.x;
        entry.gridY = position.y;
        entry.movementDistance = Mathf.Max(0f, distance);
        entry.label = string.IsNullOrWhiteSpace(label)
            ? (success ? "이동 완료" : "이동 막힘")
            : label;
        entry.sentiment = success ? 0.02f : -0.35f;
        entry.time = Now;
        AddEntry(entry);
    }

    public float GetMomentumScore(CharacterAiBranch branch)
    {
        if (branch == CharacterAiBranch.None || recentEntries.Count == 0)
        {
            return 0f;
        }

        Prune();
        CharacterAiIntentionType intention = CharacterAiUtilityText.GetIntention(branch);
        CharacterAiMemoryEntry latest = null;
        for (int i = recentEntries.Count - 1; i >= 0; i--)
        {
            CharacterAiMemoryEntry entry = recentEntries[i];
            if (entry != null)
            {
                latest = entry;
                break;
            }
        }

        if (latest == null)
        {
            return 0f;
        }

        float age = Now - latest.time;
        float recency = Mathf.Clamp01(1f - age / RecentWindowSeconds);
        if (latest.branch == branch || latest.intention == intention)
        {
            return 0.16f * recency;
        }

        return -0.05f * recency;
    }

    public float GetFacilityMemoryScore(BuildableObject building)
    {
        if (building == null)
        {
            return 0.5f;
        }

        Prune();
        float totalSentiment = 0f;
        int matchCount = 0;
        for (int i = 0; i < recentEntries.Count; i++)
        {
            CharacterAiMemoryEntry entry = recentEntries[i];
            if (entry != null && entry.facilityId == building.id)
            {
                totalSentiment += entry.sentiment;
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            return 0.5f;
        }

        float sentiment = totalSentiment / matchCount;
        return Mathf.Clamp01(0.5f + sentiment * 0.5f);
    }

    public float GetRepeatedWorkFatigue(WorkTypeId workTypeId)
    {
        if (!workTypeId.IsValid)
        {
            return 0f;
        }

        Prune();
        float now = Now;
        int repeated = 0;
        for (int i = 0; i < recentEntries.Count; i++)
        {
            CharacterAiMemoryEntry entry = recentEntries[i];
            if (entry != null
                && now - entry.time <= RecentWindowSeconds * 2f
                && TryGetEntryWorkTypeId(entry, out WorkTypeId entryWorkTypeId)
                && entryWorkTypeId == workTypeId)
            {
                repeated++;
            }
        }

        if (repeated <= 2)
        {
            return 0f;
        }

        return Mathf.Clamp01((repeated - 2) / 5f);
    }

    public float GetRecentTargetWorkFatigue(BuildableObject building, WorkTypeId workTypeId)
    {
        if (building == null || !workTypeId.IsValid)
        {
            return 0f;
        }

        Prune();
        int targetGridId = building.GridId;
        TryGetBuildingGridPosition(building, out Vector2Int targetPosition);
        float now = Now;
        float pressure = 0f;
        foreach (CharacterAiMemoryEntry entry in recentEntries)
        {
            if (entry == null
                || !TryGetEntryWorkTypeId(entry, out WorkTypeId entryWorkTypeId)
                || entryWorkTypeId != workTypeId)
            {
                continue;
            }

            bool sameTarget = targetGridId >= 0
                ? entry.targetGridId == targetGridId
                : entry.gridX == targetPosition.x && entry.gridY == targetPosition.y;
            if (!sameTarget)
            {
                continue;
            }

            float age = now - entry.time;
            if (age > 36f)
            {
                continue;
            }

            pressure += Mathf.Lerp(0.16f, 0.42f, Mathf.Clamp01(1f - age / 36f));
        }

        return Mathf.Clamp01(pressure);
    }

    private static bool TryGetEntryWorkTypeId(
        CharacterAiMemoryEntry entry,
        out WorkTypeId workTypeId)
    {
        workTypeId = default;
        string id = entry?.workTypeId;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        workTypeId = new WorkTypeId(id);
        return true;
    }

    public float GetRecentFailurePressure()
    {
        Prune();
        float now = Now;
        float pressure = 0f;
        foreach (CharacterAiMemoryEntry entry in recentEntries)
        {
            if (entry == null)
            {
                continue;
            }

            float age = now - entry.time;
            if (age > RecentWindowSeconds * 2f)
            {
                continue;
            }

            bool failed = entry.failureKind != AIActionFailureKind.None
                || entry.sentiment <= -0.3f;
            if (!failed)
            {
                continue;
            }

            pressure += Mathf.Lerp(0.04f, 0.24f, Mathf.Clamp01(1f - age / (RecentWindowSeconds * 2f)));
        }

        return Mathf.Clamp01(pressure);
    }

    public float GetRecentMovementPressure()
    {
        Prune();
        float now = Now;
        float movement = 0f;
        foreach (CharacterAiMemoryEntry entry in recentEntries)
        {
            if (entry == null || entry.movementDistance <= 0f)
            {
                continue;
            }

            float age = now - entry.time;
            if (age > RecentWindowSeconds)
            {
                continue;
            }

            movement += entry.movementDistance * Mathf.Clamp01(1f - age / RecentWindowSeconds);
        }

        return Mathf.Clamp01(movement / 22f);
    }

    public string GetRecentMemorySummary(int maxEntries = 6)
    {
        Prune();
        if (recentEntries.Count == 0)
        {
            return "최근 AI 기억 없음";
        }

        return string.Join(
            "\n",
            recentEntries
                .OrderByDescending(entry => entry.time)
                .Take(Mathf.Max(1, maxEntries))
                .Select(entry =>
                {
                    float age = Mathf.Max(0f, Now - entry.time);
                    string prefix = CharacterAiUtilityText.GetIntentionLabel(entry.intention);
                    return $"{prefix}: {entry.label} ({age:0}s 전)";
                }));
    }

    private void AddEntry(CharacterAiMemoryEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        recentEntries ??= new List<CharacterAiMemoryEntry>();
        bool wasEmpty = recentEntries.Count == 0;
        recentEntries.Add(entry);
        if (wasEmpty)
        {
            nextPruneAt = entry.time + RecentWindowSeconds * 6f;
        }
    }

    private CharacterAiMemoryEntry AcquireEntry()
    {
        recentEntries ??= new List<CharacterAiMemoryEntry>();
        CharacterAiMemoryEntry entry;
        if (recentEntries.Count >= MaxEntries)
        {
            entry = recentEntries[0];
            recentEntries.RemoveAt(0);
            nextPruneAt = 0f;
        }
        else
        {
            entry = new CharacterAiMemoryEntry();
        }

        ResetEntry(entry);
        return entry;
    }

    private static void ResetEntry(CharacterAiMemoryEntry entry)
    {
        entry.branch = CharacterAiBranch.None;
        entry.intention = CharacterAiIntentionType.None;
        entry.failureKind = AIActionFailureKind.None;
        entry.facilityId = -1;
        entry.targetGridId = -1;
        entry.workTypeId = string.Empty;
        entry.gridX = 0;
        entry.gridY = 0;
        entry.label = string.Empty;
        entry.movementDistance = 0f;
        entry.sentiment = 0f;
        entry.time = 0f;
    }

    private void Prune()
    {
        if (recentEntries == null || recentEntries.Count == 0)
        {
            nextPruneAt = float.PositiveInfinity;
            return;
        }

        float now = Now;
        if (now < nextPruneAt)
        {
            return;
        }

        float retentionSeconds = RecentWindowSeconds * 6f;
        float oldest = now - retentionSeconds;
        float nextExpiry = float.PositiveInfinity;
        for (int i = recentEntries.Count - 1; i >= 0; i--)
        {
            CharacterAiMemoryEntry entry = recentEntries[i];
            if (entry == null || entry.time < oldest)
            {
                recentEntries.RemoveAt(i);
                continue;
            }

            nextExpiry = Mathf.Min(
                nextExpiry,
                entry.time + retentionSeconds);
        }

        nextPruneAt = nextExpiry;
    }

    private static bool TryGetBuildingGridPosition(BuildableObject building, out Vector2Int position)
    {
        position = Vector2Int.zero;
        if (building == null)
        {
            return false;
        }

        if (building.buildPoses != null && building.buildPoses.Count > 0)
        {
            position = building.buildPoses[0];
            return true;
        }

        position = building.centerPos;
        return true;
    }
}
