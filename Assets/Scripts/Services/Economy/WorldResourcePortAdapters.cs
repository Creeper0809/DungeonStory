using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class WorldResourceEnvironmentPortAdapter :
    IWorldResourceEnvironmentPort
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly WildlifeEcosystemApplicationAdapter ecosystemRuntime;
    private Grid observedGrid;
    private int worldRevision;

    public WorldResourceEnvironmentPortAdapter(
        IGridSystemProvider gridSystemProvider,
        WildlifeEcosystemApplicationAdapter ecosystemRuntime)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.ecosystemRuntime = ecosystemRuntime
            ?? throw new ArgumentNullException(nameof(ecosystemRuntime));
    }

    public bool TryCaptureTopology(out WorldResourceTopologySnapshot topology)
    {
        topology = null;
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        ecosystemRuntime.EnsureInitialized(grid);
        WildlifeHabitatDecorationRuntime decorations =
            ecosystemRuntime.DecorationRuntime;
        if (!decorations.IsReady)
        {
            return false;
        }

        if (!ReferenceEquals(observedGrid, grid))
        {
            observedGrid = grid;
            unchecked
            {
                worldRevision++;
            }
        }

        topology = new WorldResourceTopologySnapshot(
            worldRevision,
            decorations.StructureVersion,
            decorations.GetResourceVisuals()
                .Select(visual => new WorldResourceVisualSnapshot(
                    visual.VisualId,
                    visual.Position,
                    visual.Kind == NaturalResourceVisualKind.Tree
                        ? WorldResourceVisualKind.Tree
                        : WorldResourceVisualKind.Rock))
                .ToArray(),
            ecosystemRuntime.Patches
                .Where(patch => patch != null
                    && patch.HabitatType is WildlifeHabitatType.Grass
                        or WildlifeHabitatType.Brush)
                .Select(ToSnapshot)
                .ToArray());
        return true;
    }

    public bool TryGetRenewablePatch(
        WildlifeHabitatPatchId patchId,
        out WorldResourceRenewablePatchSnapshot patch)
    {
        WildlifeHabitatPatch source = FindPatch(patchId);
        patch = source == null ? default : ToSnapshot(source);
        return source != null;
    }

    public float ConsumeRenewablePatch(
        WildlifeHabitatPatchId patchId,
        float amount) => FindPatch(patchId)?.Consume(amount) ?? 0f;

    public void RefreshRenewablePatch(WildlifeHabitatPatchId patchId)
    {
        WildlifeHabitatPatch patch = FindPatch(patchId);
        if (patch != null)
        {
            ecosystemRuntime.DecorationRuntime.RefreshPatch(patch);
        }
    }

    public void SetResourceVisualActive(string visualId, bool active) =>
        ecosystemRuntime.DecorationRuntime.SetResourceVisualActive(
            visualId,
            active);

    private WildlifeHabitatPatch FindPatch(WildlifeHabitatPatchId patchId) =>
        patchId.IsValid
            ? ecosystemRuntime.Patches.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.PatchId,
                    patchId.Value,
                    StringComparison.Ordinal))
            : null;

    private static WorldResourceRenewablePatchSnapshot ToSnapshot(
        WildlifeHabitatPatch patch) => new(
            (WildlifeHabitatPatchId)patch.PatchId,
            patch.Center,
            patch.CurrentResource,
            patch.Resource01);
}

internal sealed class WorldResourceFacilityHost :
    Facility,
    IWorldResourceNodeHost
{
    public WorldResourceNode ResourceNode { get; private set; }

    public void Bind(WorldResourceNode resourceNode)
    {
        ResourceNode = resourceNode
            ?? throw new ArgumentNullException(nameof(resourceNode));
    }

    public override bool isVisitable() =>
        ResourceNode != null && !IsGridDestroyed;
}

public sealed class WorldResourceNodeHostPortAdapter :
    IWorldResourceNodeHostPort
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IObjectResolver objectResolver;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IRuntimeBuildingArchetypeCatalog buildingArchetypes;

    public WorldResourceNodeHostPortAdapter(
        IGridSystemProvider gridSystemProvider,
        IObjectResolver objectResolver,
        IFacilityCandidateCache facilityCandidateCache,
        IWorkforceReplanService workforceReplanService,
        IRuntimeBuildingArchetypeCatalog buildingArchetypes)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.workforceReplanService = workforceReplanService
            ?? throw new ArgumentNullException(nameof(workforceReplanService));
        this.buildingArchetypes = buildingArchetypes
            ?? throw new ArgumentNullException(nameof(buildingArchetypes));
    }

    public WorldResourceNode CreateNode(
        IWorldResourceRuntime runtime,
        BuildingInstanceId nodeId,
        Vector2Int position,
        string displayName)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            throw new InvalidOperationException(
                "World-resource node creation requires an initialized grid.");
        }

        GameObject target = new();
        DungeonRuntimeHierarchy.Parent(target, DungeonRuntimeHierarchy.Exterior);
        BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.9f, 0.9f);
        WorldResourceFacilityHost host =
            target.AddComponent<WorldResourceFacilityHost>();
        WorldResourceNode node = target.AddComponent<WorldResourceNode>();
        objectResolver.InjectGameObject(target);
        host.RestorePersistentIdentity(nodeId);
        host.SetGrid(grid);
        host.Initialization(buildingArchetypes.WorldResourceNode, position);
        node.Configure(runtime, nodeId, displayName);
        host.Bind(node);
        Vector3 world = grid.GetWorldPos(position);
        target.transform.position = new Vector3(
            world.x,
            world.y + 0.5f,
            -0.02f);
        return node;
    }

    public void DestroyNode(WorldResourceNode node)
    {
        if (node != null
            && node.TryGetComponent(out WorldResourceFacilityHost host))
        {
            host.DestroySelf();
        }
    }

    public void MarkDynamicStateDirty() =>
        facilityCandidateCache.MarkDynamicStateDirty();

    public void ResetCandidatesAndReplan()
    {
        facilityCandidateCache.Clear();
        workforceReplanService.RequestIdleWorkersToReplan();
    }
}

public sealed class WorldResourceOutputPortAdapter : IWorldResourceOutputPort
{
    private readonly IProductionItemGateway itemGateway;

    public WorldResourceOutputPortAdapter(IProductionItemGateway itemGateway)
    {
        this.itemGateway = itemGateway
            ?? throw new ArgumentNullException(nameof(itemGateway));
    }

    public bool CanSpawnOutput(
        string itemId,
        int amount,
        Vector2Int position,
        out DomainFailure failure) => itemGateway.CanSpawnOutput(
            itemId,
            amount,
            position,
            out failure);

    public bool SpawnOutput(
        string itemId,
        int amount,
        Vector2Int position) => itemGateway.SpawnOutput(
            itemId,
            amount,
            position);
}

public sealed class WorldResourceResearchPortAdapter : IWorldResourceResearchPort
{
    private readonly BlueprintResearchRuntime research;

    public WorldResourceResearchPortAdapter(
        ProgressionSceneRuntimeReferences progressionRuntimes)
    {
        research = (progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                "World-resource research requires a loaded blueprint runtime.");
    }

    public bool IsCompleted(string researchId) =>
        research.State.Projects.IsCompleted(
            new ResearchProjectId(researchId));
}
