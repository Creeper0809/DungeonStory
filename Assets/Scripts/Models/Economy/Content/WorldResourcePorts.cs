using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public enum WorldResourceVisualKind
{
    Tree = 0,
    Rock = 1
}

public readonly struct WorldResourceVisualSnapshot
{
    public WorldResourceVisualSnapshot(
        string visualId,
        Vector2Int position,
        WorldResourceVisualKind kind)
    {
        VisualId = visualId ?? string.Empty;
        Position = position;
        Kind = kind;
    }

    public string VisualId { get; }
    public Vector2Int Position { get; }
    public WorldResourceVisualKind Kind { get; }
}

public readonly struct WorldResourceRenewablePatchSnapshot
{
    public WorldResourceRenewablePatchSnapshot(
        WildlifeHabitatPatchId patchId,
        WildlifeHabitatType habitatType,
        Vector2Int position,
        float currentResource,
        float resourceRatio)
    {
        PatchId = patchId;
        HabitatType = habitatType;
        Position = position;
        CurrentResource = Mathf.Max(0f, currentResource);
        ResourceRatio = Mathf.Clamp01(resourceRatio);
    }

    public WildlifeHabitatPatchId PatchId { get; }
    public WildlifeHabitatType HabitatType { get; }
    public Vector2Int Position { get; }
    public float CurrentResource { get; }
    public float ResourceRatio { get; }
}

public readonly struct WorldResourceRenewableDebitReceipt
{
    public WorldResourceRenewableDebitReceipt(
        WildlifeHabitatPatchId patchId,
        float amount,
        float beforeResource,
        float afterResource)
    {
        PatchId = patchId;
        Amount = amount;
        BeforeResource = beforeResource;
        AfterResource = afterResource;
    }

    public WildlifeHabitatPatchId PatchId { get; }
    public float Amount { get; }
    public float BeforeResource { get; }
    public float AfterResource { get; }
    public bool IsValid => PatchId.IsValid
        && Amount > 0f
        && BeforeResource >= Amount
        && AfterResource >= 0f
        && BeforeResource > AfterResource;
}

public sealed class WorldResourceTopologySnapshot
{
    public WorldResourceTopologySnapshot(
        int worldRevision,
        int structureVersion,
        IReadOnlyList<WorldResourceVisualSnapshot> visuals,
        IReadOnlyList<WorldResourceRenewablePatchSnapshot> renewablePatches)
    {
        WorldRevision = worldRevision;
        StructureVersion = structureVersion;
        Visuals = visuals ?? throw new ArgumentNullException(nameof(visuals));
        RenewablePatches = renewablePatches
            ?? throw new ArgumentNullException(nameof(renewablePatches));
    }

    public int WorldRevision { get; }
    public int StructureVersion { get; }
    public IReadOnlyList<WorldResourceVisualSnapshot> Visuals { get; }
    public IReadOnlyList<WorldResourceRenewablePatchSnapshot> RenewablePatches
    {
        get;
    }
}

public interface IWorldResourceEnvironmentPort
{
    bool TryCaptureTopology(out WorldResourceTopologySnapshot topology);
    bool TryGetRenewablePatch(
        WildlifeHabitatPatchId patchId,
        out WorldResourceRenewablePatchSnapshot patch);
    bool TryConsumeRenewablePatchExact(
        WildlifeHabitatPatchId patchId,
        float amount,
        out WorldResourceRenewableDebitReceipt receipt);
    bool TryRollbackRenewablePatchDebit(
        WorldResourceRenewableDebitReceipt receipt);
    void RefreshRenewablePatch(WildlifeHabitatPatchId patchId);
    void SetResourceVisualActive(string visualId, bool active);
}

public interface IWorldResourceNodeHost
{
    WorldResourceNode ResourceNode { get; }
}

public interface IWorldResourceNodeHostPort
{
    WorldResourceNode CreateNode(
        IWorldResourceRuntime runtime,
        BuildingInstanceId nodeId,
        Vector2Int position,
        string displayName);
    void DestroyNode(WorldResourceNode node);
    void MarkDynamicStateDirty();
    void ResetCandidatesAndReplan();
}

public interface IWorldResourceResearchPort
{
    bool IsCompleted(string researchId);
}
