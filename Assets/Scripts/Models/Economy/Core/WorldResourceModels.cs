using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct WorldResourceWorkSnapshot
{
    public WorldResourceWorkSnapshot(
        string nodeId,
        WorkTypeId workTypeId,
        string recipeId,
        string displayName,
        float requiredWork,
        float completedWork,
        float resourceRatio,
        bool available,
        string unavailableReason)
    {
        NodeId = nodeId ?? string.Empty;
        WorkTypeId = workTypeId;
        RecipeId = recipeId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        RequiredWork = Mathf.Max(0.1f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        ResourceRatio = Mathf.Clamp01(resourceRatio);
        Available = available;
        UnavailableReason = unavailableReason ?? string.Empty;
    }

    public string NodeId { get; }
    public WorkTypeId WorkTypeId { get; }
    public string RecipeId { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public float ResourceRatio { get; }
    public bool Available { get; }
    public string UnavailableReason { get; }
}

public interface IWorldResourceRuntime
{
    int Version { get; }
    int NodeCount { get; }
    IReadOnlyList<WorldResourceNode> Nodes { get; }

    bool TryGetWork(
        WorldResourceNode node,
        WorkTypeId workTypeId,
        out WorldResourceWorkSnapshot snapshot);

    bool ApplyWork(
        WorldResourceNode node,
        WorkTypeId workTypeId,
        float amount,
        out bool cycleCompleted);

    DungeonWorldResourceSaveData Capture();
    void Restore(DungeonWorldResourceSaveData saveData);
}

[Serializable]
public sealed class WorldResourceSourceSaveData
{
    public string workTypeId = string.Empty;
    public string recipeId = string.Empty;
    public float completedWork;
    public int remainingCycles;
}

[Serializable]
public sealed class WorldResourceNodeSaveData
{
    public string nodeId = string.Empty;
    public List<WorldResourceSourceSaveData> sources =
        new List<WorldResourceSourceSaveData>();
}

[Serializable]
public sealed class DungeonWorldResourceSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<WorldResourceNodeSaveData> nodes =
        new List<WorldResourceNodeSaveData>();
}
