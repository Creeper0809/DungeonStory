using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

internal sealed class WorldResourceSourceState
{
    public WorkTypeId WorkTypeId;
    public string RecipeId = string.Empty;
    public float CompletedWork;
    public int RemainingCycles;
    public WildlifeHabitatPatch Patch;
    public readonly List<string> VisualIds = new List<string>();

    public bool IsRenewablePatch => Patch != null;
}

internal sealed class WorldResourceNodeState
{
    public string NodeId = string.Empty;
    public Vector2Int Position;
    public WorldResourceNode Node;
    public readonly Dictionary<WorkTypeId, WorldResourceSourceState> Sources =
        new Dictionary<WorkTypeId, WorldResourceSourceState>();
}

public sealed class WorldResourceRuntime :
    IWorldResourceRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private const int SyntheticBuildingId = -9800;
    private const float PatchHarvestAmount = 1.5f;

    private static readonly WorkTypeId[] SupportedWorkTypes =
    {
        BuiltInWorkTypeIds.Gather,
        BuiltInWorkTypeIds.Logging,
        BuiltInWorkTypeIds.Quarry
    };

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly WildlifeEcosystemRuntime ecosystemRuntime;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway itemGateway;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IObjectResolver objectResolver;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IRandomStream random;
    private readonly Dictionary<string, WorldResourceNodeState> statesById =
        new Dictionary<string, WorldResourceNodeState>(StringComparer.Ordinal);
    private readonly Dictionary<WorldResourceNode, WorldResourceNodeState> statesByNode =
        new Dictionary<WorldResourceNode, WorldResourceNodeState>();
    private readonly List<WorldResourceNode> nodeView = new List<WorldResourceNode>();

    private DungeonWorldResourceSaveData pendingRestore;
    private BuildingSO syntheticBuilding;
    private Grid initializedGrid;
    private int initializedDecorationVersion = -1;

    public WorldResourceRuntime(
        IGridSystemProvider gridSystemProvider,
        WildlifeEcosystemRuntime ecosystemRuntime,
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway itemGateway,
        IRandomStreamProvider randomStreamProvider,
        IObjectResolver objectResolver,
        IFacilityCandidateCache facilityCandidateCache,
        IBlueprintResearchRuntimeProvider researchProvider = null,
        IWorkforceReplanService workforceReplanService = null,
        IGrandProjectBenefitQuery grandProjectBenefits = null)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.ecosystemRuntime = ecosystemRuntime
            ?? throw new ArgumentNullException(nameof(ecosystemRuntime));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemGateway = itemGateway ?? throw new ArgumentNullException(nameof(itemGateway));
        this.objectResolver = objectResolver ?? throw new ArgumentNullException(nameof(objectResolver));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        random = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("economy:world-resources");
        this.researchProvider = researchProvider;
        this.workforceReplanService = workforceReplanService;
        this.grandProjectBenefits = grandProjectBenefits;
    }

    public int Version { get; private set; }
    public int NodeCount => nodeView.Count;
    public IReadOnlyList<WorldResourceNode> Nodes => nodeView;

    public void Initialize()
    {
    }

    public void Tick()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        ecosystemRuntime.EnsureInitialized(grid);
        WildlifeHabitatDecorationRuntime decorations = ecosystemRuntime.DecorationRuntime;
        if (!decorations.IsReady)
        {
            return;
        }

        int decorationVersion = decorations.StructureVersion;
        if (ReferenceEquals(initializedGrid, grid)
            && initializedDecorationVersion == decorationVersion)
        {
            return;
        }

        IReadOnlyList<NaturalResourceVisualSnapshot> visuals =
            decorations.GetResourceVisuals();
        Rebuild(grid, visuals, decorationVersion);
    }

    public void Dispose()
    {
        ClearNodes();
        if (syntheticBuilding != null)
        {
            DestroyObject(syntheticBuilding);
            syntheticBuilding = null;
        }
    }

    public bool TryGetWork(
        WorldResourceNode node,
        WorkTypeId workTypeId,
        out WorldResourceWorkSnapshot snapshot)
    {
        snapshot = default;
        if (!TryGetNodeState(node, out WorldResourceNodeState nodeState)
            || !nodeState.Sources.TryGetValue(workTypeId, out WorldResourceSourceState source)
            || !catalog.TryGetRecipe(source.RecipeId, out ProductionRecipeSO recipe))
        {
            return false;
        }

        bool hasResource = source.IsRenewablePatch
            ? source.Patch.CurrentResource >= PatchHarvestAmount
            : source.RemainingCycles > 0;
        bool researchUnlocked = IsResearchUnlocked(recipe, out string researchReason);
        bool available = hasResource && researchUnlocked;
        string reason = available
            ? string.Empty
            : !hasResource
                ? source.IsRenewablePatch
                    ? "채집할 자원이 다시 자라는 중"
                    : "자원이 고갈됨"
                : researchReason;
        float resourceRatio = source.IsRenewablePatch
            ? source.Patch.Resource01
            : Mathf.Clamp01(source.RemainingCycles);
        snapshot = new WorldResourceWorkSnapshot(
            nodeState.NodeId,
            workTypeId,
            recipe.RecipeId,
            recipe.DisplayName,
            recipe.RequiredWork,
            source.CompletedWork,
            resourceRatio,
            available,
            reason);
        return true;
    }

    public bool ApplyWork(
        WorldResourceNode node,
        WorkTypeId workTypeId,
        float amount,
        out bool cycleCompleted)
    {
        cycleCompleted = false;
        if (amount <= 0f
            || !TryGetNodeState(node, out WorldResourceNodeState nodeState)
            || !nodeState.Sources.TryGetValue(workTypeId, out WorldResourceSourceState source)
            || !TryGetWork(node, workTypeId, out WorldResourceWorkSnapshot snapshot)
            || !snapshot.Available
            || !catalog.TryGetRecipe(source.RecipeId, out ProductionRecipeSO recipe))
        {
            return false;
        }

        source.CompletedWork = Mathf.Min(
            recipe.RequiredWork,
            source.CompletedWork + Mathf.Max(0f, amount));
        Version++;
        if (source.CompletedWork + 0.001f < recipe.RequiredWork)
        {
            return true;
        }

        if (source.IsRenewablePatch)
        {
            if (source.Patch.Consume(PatchHarvestAmount) + 0.001f < PatchHarvestAmount)
            {
                source.CompletedWork = 0f;
                return false;
            }

            ecosystemRuntime.DecorationRuntime.RefreshPatch(source.Patch);
        }
        else
        {
            source.RemainingCycles = Mathf.Max(0, source.RemainingCycles - 1);
            if (source.RemainingCycles == 0)
            {
                foreach (string visualId in source.VisualIds)
                {
                    ecosystemRuntime.DecorationRuntime.SetResourceVisualActive(visualId, false);
                }
            }
        }

        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            if (output != null
                && output.Probability > 0f
                && random.Chance(output.Probability))
            {
                float multiplier =
                    grandProjectBenefits?.GetProductionOutputMultiplier(
                        recipe.FacilityTag) ?? 1f;
                itemGateway.SpawnOutput(
                    output.ItemId,
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(output.Amount * multiplier)),
                    nodeState.Position);
            }
        }

        source.CompletedWork = 0f;
        cycleCompleted = true;
        Version++;
        facilityCandidateCache.MarkDynamicStateDirty();
        return true;
    }

    private bool TryGetNodeState(
        WorldResourceNode node,
        out WorldResourceNodeState state)
    {
        state = null;
        return node != null
            && (statesByNode.TryGetValue(node, out state)
                || (!string.IsNullOrWhiteSpace(node.NodeId)
                    && statesById.TryGetValue(node.NodeId, out state)));
    }

    public DungeonWorldResourceSaveData Capture()
    {
        DungeonWorldResourceSaveData data = new DungeonWorldResourceSaveData();
        foreach (WorldResourceNodeState state in statesById.Values
                     .OrderBy(entry => entry.NodeId, StringComparer.Ordinal))
        {
            WorldResourceNodeSaveData node = new WorldResourceNodeSaveData
            {
                nodeId = state.NodeId
            };
            foreach (WorldResourceSourceState source in state.Sources.Values
                         .OrderBy(entry => entry.WorkTypeId.Value, StringComparer.Ordinal))
            {
                node.sources.Add(new WorldResourceSourceSaveData
                {
                    workTypeId = source.WorkTypeId.Value,
                    recipeId = source.RecipeId,
                    completedWork = source.CompletedWork,
                    remainingCycles = source.RemainingCycles
                });
            }

            data.nodes.Add(node);
        }

        return data;
    }

    public void Restore(DungeonWorldResourceSaveData saveData)
    {
        pendingRestore = saveData ?? new DungeonWorldResourceSaveData();
        if (statesById.Count > 0)
        {
            ApplyRestore(pendingRestore);
            pendingRestore = null;
        }
    }

    private void Rebuild(
        Grid grid,
        IReadOnlyList<NaturalResourceVisualSnapshot> visuals,
        int decorationVersion)
    {
        DungeonWorldResourceSaveData retained = pendingRestore ?? Capture();
        ClearNodes();
        initializedGrid = grid;
        initializedDecorationVersion = decorationVersion;

        Dictionary<Vector2Int, List<NaturalResourceVisualSnapshot>> visualsByCell =
            visuals
                .GroupBy(visual => visual.Position)
                .ToDictionary(group => group.Key, group => group.ToList());
        foreach (KeyValuePair<Vector2Int, List<NaturalResourceVisualSnapshot>> entry in visualsByCell)
        {
            foreach (IGrouping<NaturalResourceVisualKind, NaturalResourceVisualSnapshot> kindGroup
                     in entry.Value.GroupBy(visual => visual.Kind))
            {
                WorkTypeId workTypeId = kindGroup.Key == NaturalResourceVisualKind.Tree
                    ? BuiltInWorkTypeIds.Logging
                    : BuiltInWorkTypeIds.Quarry;
                string recipeId = kindGroup.Key == NaturalResourceVisualKind.Tree
                    ? "source:logging"
                    : "source:saltstone";
                WorldResourceSourceState source = GetOrCreateSource(
                    entry.Key,
                    workTypeId,
                    recipeId,
                    renewablePatch: null);
                source.RemainingCycles = Mathf.Max(1, kindGroup.Count());
                source.VisualIds.AddRange(kindGroup.Select(visual => visual.VisualId));
            }
        }

        foreach (WildlifeHabitatPatch patch in ecosystemRuntime.Patches
                     .Where(candidate => candidate != null
                         && candidate.HabitatType is WildlifeHabitatType.Grass or WildlifeHabitatType.Brush))
        {
            GetOrCreateSource(
                patch.Center,
                BuiltInWorkTypeIds.Gather,
                "source:grass",
                patch);
        }

        ApplyRestore(retained);
        pendingRestore = null;
        foreach (WorldResourceNodeState state in statesById.Values)
        {
            CreateNode(grid, state);
            ApplyVisualState(state);
        }

        Version++;
        facilityCandidateCache.Clear();
        workforceReplanService?.RequestIdleWorkersToReplan();
    }

    private WorldResourceSourceState GetOrCreateSource(
        Vector2Int position,
        WorkTypeId workTypeId,
        string recipeId,
        WildlifeHabitatPatch renewablePatch)
    {
        string nodeId = $"natural:{position.x}:{position.y}";
        if (!statesById.TryGetValue(nodeId, out WorldResourceNodeState node))
        {
            node = new WorldResourceNodeState
            {
                NodeId = nodeId,
                Position = position
            };
            statesById.Add(nodeId, node);
        }

        if (!node.Sources.TryGetValue(workTypeId, out WorldResourceSourceState source))
        {
            source = new WorldResourceSourceState
            {
                WorkTypeId = workTypeId,
                RecipeId = recipeId,
                RemainingCycles = renewablePatch == null ? 1 : -1,
                Patch = renewablePatch
            };
            node.Sources.Add(workTypeId, source);
        }

        return source;
    }

    private void CreateNode(Grid grid, WorldResourceNodeState state)
    {
        GameObject target = new GameObject();
        DungeonRuntimeHierarchy.Parent(target, DungeonRuntimeHierarchy.Exterior);
        BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.9f, 0.9f);
        WorldResourceNode node = target.AddComponent<WorldResourceNode>();
        objectResolver.InjectGameObject(target);
        node.SetGrid(grid);
        node.Initialization(GetSyntheticBuilding(), state.Position);
        node.Configure(this, state.NodeId, ResolveNodeDisplayName(state));
        Vector3 world = grid.GetWorldPos(state.Position);
        target.transform.position = new Vector3(world.x, world.y + 0.5f, -0.02f);
        state.Node = node;
        statesByNode[node] = state;
        nodeView.Add(node);
    }

    private BuildingSO GetSyntheticBuilding()
    {
        if (syntheticBuilding != null)
        {
            return syntheticBuilding;
        }

        syntheticBuilding = ScriptableObject.CreateInstance<BuildingSO>();
        syntheticBuilding.hideFlags = HideFlags.HideAndDontSave;
        syntheticBuilding.id = SyntheticBuildingId;
        syntheticBuilding.objectName = "외부 자원";
        syntheticBuilding.width = 1;
        syntheticBuilding.height = 1;
        syntheticBuilding.layer = GridLayer.FloorOverlay;
        syntheticBuilding.category = BuildingCategory.Resource;
        syntheticBuilding.type = typeof(WorldResourceNode);
        syntheticBuilding.unlocked = true;
        FacilityData facility = new FacilityData
        {
            roles = FacilityRole.None,
            capacity = 0,
            useDuration = 0f,
            requiredWorkers = 1,
            disabledWhenDamaged = false
        };
        facility.SetSupportedWorkTypeIds(SupportedWorkTypes);
        syntheticBuilding.Facility = facility;
        syntheticBuilding.AbilityModules.EnsureStableIds();
        syntheticBuilding.ValidateAbilitiesOrThrow();
        return syntheticBuilding;
    }

    private void ApplyRestore(DungeonWorldResourceSaveData saveData)
    {
        if (saveData?.nodes == null)
        {
            return;
        }

        foreach (WorldResourceNodeSaveData savedNode in saveData.nodes)
        {
            if (savedNode == null
                || !statesById.TryGetValue(savedNode.nodeId ?? string.Empty, out WorldResourceNodeState state)
                || savedNode.sources == null)
            {
                continue;
            }

            foreach (WorldResourceSourceSaveData savedSource in savedNode.sources)
            {
                WorkTypeId workTypeId = new WorkTypeId(savedSource?.workTypeId);
                if (savedSource == null
                    || !workTypeId.IsValid
                    || !state.Sources.TryGetValue(workTypeId, out WorldResourceSourceState source)
                    || !string.Equals(source.RecipeId, savedSource.recipeId, StringComparison.Ordinal)
                    || !catalog.TryGetRecipe(source.RecipeId, out ProductionRecipeSO recipe))
                {
                    continue;
                }

                source.CompletedWork = Mathf.Clamp(
                    savedSource.completedWork,
                    0f,
                    recipe.RequiredWork);
                if (!source.IsRenewablePatch)
                {
                    source.RemainingCycles = Mathf.Clamp(
                        savedSource.remainingCycles,
                        0,
                        Mathf.Max(1, source.VisualIds.Count));
                }
            }
        }
    }

    private void ApplyVisualState(WorldResourceNodeState state)
    {
        foreach (WorldResourceSourceState source in state.Sources.Values)
        {
            if (source.IsRenewablePatch)
            {
                ecosystemRuntime.DecorationRuntime.RefreshPatch(source.Patch);
                continue;
            }

            bool active = source.RemainingCycles > 0;
            foreach (string visualId in source.VisualIds)
            {
                ecosystemRuntime.DecorationRuntime.SetResourceVisualActive(visualId, active);
            }
        }
    }

    private bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out string reason)
    {
        reason = string.Empty;
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.RequiredResearchId))
        {
            return true;
        }

        if (researchProvider == null
            || !researchProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
            || !runtime.State.Projects.IsCompleted(
                new ResearchProjectId(recipe.RequiredResearchId)))
        {
            reason = $"연구 필요: {recipe.RequiredResearchId}";
            return false;
        }

        return true;
    }

    private void ClearNodes()
    {
        foreach (WorldResourceNode node in nodeView.ToArray())
        {
            if (node != null)
            {
                node.DestroySelf();
            }
        }

        nodeView.Clear();
        statesByNode.Clear();
        statesById.Clear();
    }

    private static string ResolveNodeDisplayName(WorldResourceNodeState state)
    {
        bool hasTree = state.Sources.ContainsKey(BuiltInWorkTypeIds.Logging);
        bool hasRock = state.Sources.ContainsKey(BuiltInWorkTypeIds.Quarry);
        bool hasGrass = state.Sources.ContainsKey(BuiltInWorkTypeIds.Gather);
        if (hasTree && hasGrass) return "꽃이 핀 나무 군락";
        if (hasTree) return "외부 나무";
        if (hasRock) return "외부 암석";
        return "채집 풀밭";
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
