using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

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

}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldResourceSourceSaveData
{
    public string workTypeId = string.Empty;
    public string recipeId = string.Empty;
    public float completedWork;
    public int remainingCycles;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldResourceNodeSaveData
{
    public string buildingInstanceId = string.Empty;
    public int gridX;
    public int gridY;
    public List<WorldResourceSourceSaveData> sources =
        new List<WorldResourceSourceSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonWorldResourceSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<WorldResourceNodeSaveData> nodes =
        new List<WorldResourceNodeSaveData>();
}

public sealed class WorldResourceRestoreCandidate
{
    internal WorldResourceRestoreCandidate(WorldResourceAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal WorldResourceAggregateState State { get; }
}

public interface IWorldResourcePersistence
{
    DungeonWorldResourceSaveData Capture();
    WorldResourceRestoreCandidate BuildRestore(
        DungeonWorldResourceSaveData saveData);
    void Restore(WorldResourceRestoreCandidate candidate);
}
