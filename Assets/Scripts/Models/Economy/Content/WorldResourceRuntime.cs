using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class WorldResourceSourceState
{
    public WorkTypeId WorkTypeId;
    public string RecipeId = string.Empty;
    public float CompletedWork;
    public int RemainingCycles;
    public WildlifeHabitatPatchId PatchId;
    public readonly List<string> VisualIds = new List<string>();

    public bool IsRenewablePatch => PatchId.IsValid;
}

internal sealed class WorldResourceNodeState
{
    public BuildingInstanceId NodeId;
    public Vector2Int Position;
    public WorldResourceNode Node;
    public readonly Dictionary<WorkTypeId, WorldResourceSourceState> Sources =
        new Dictionary<WorkTypeId, WorldResourceSourceState>();
}

internal sealed class WorldResourceAggregateState
{
    internal Dictionary<BuildingInstanceId, WorldResourceNodeState> StatesById { get; } =
        new();
    internal Dictionary<WorldResourceNode, WorldResourceNodeState> StatesByNode { get; } =
        new();
    internal List<WorldResourceNode> NodeView { get; } = new();
    internal int InitializedWorldRevision { get; set; } = -1;
    internal int InitializedDecorationVersion { get; set; } = -1;
    internal bool RequireExactRebind { get; set; }
    internal int Version { get; set; }
}

public sealed class WorldResourceEnvironmentDependencies
{
    public WorldResourceEnvironmentDependencies(
        IWorldResourceEnvironmentPort environment,
        IResourceEconomyContentCatalog catalog,
        IWorldResourceOutputPort output)
    {
        Environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public IWorldResourceEnvironmentPort Environment { get; }
    public IResourceEconomyContentCatalog Catalog { get; }
    public IWorldResourceOutputPort Output { get; }
}

public sealed class WorldResourceInfrastructureDependencies
{
    public WorldResourceInfrastructureDependencies(
        IWorldResourceNodeHostPort nodeHosts)
    {
        NodeHosts = nodeHosts
            ?? throw new ArgumentNullException(nameof(nodeHosts));
    }

    public IWorldResourceNodeHostPort NodeHosts { get; }
}

public sealed class WorldResourceProgressionDependencies
{
    public WorldResourceProgressionDependencies(
        IRandomStreamProvider randomStreamProvider,
        IPersistentIdGenerator persistentIds,
        IWorldResourceResearchPort research,
        IGrandProjectBenefitQuery grandProjectBenefits)
    {
        RandomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        PersistentIds = persistentIds
            ?? throw new ArgumentNullException(nameof(persistentIds));
        Research = research ?? throw new ArgumentNullException(nameof(research));
        GrandProjectBenefits = grandProjectBenefits
            ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
    }

    public IRandomStreamProvider RandomStreamProvider { get; }
    public IPersistentIdGenerator PersistentIds { get; }
    public IWorldResourceResearchPort Research { get; }
    public IGrandProjectBenefitQuery GrandProjectBenefits { get; }
}

public sealed class WorldResourceRuntime :
    IWorldResourceRuntime,
    IWorldResourcePersistence,
    IInitializable,
    ITickable,
    IDisposable
{
    private const float PatchHarvestAmount = 1.5f;

    private readonly IWorldResourceEnvironmentPort environment;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldResourceOutputPort outputPort;
    private readonly IWorldResourceResearchPort research;
    private readonly IWorldResourceNodeHostPort nodeHosts;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IRandomStream random;
    private readonly IPersistentIdGenerator persistentIds;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private WorldResourceAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new WorldResourceAggregateState());
    private Dictionary<BuildingInstanceId, WorldResourceNodeState> statesById =>
        aggregateState.StatesById;
    private Dictionary<WorldResourceNode, WorldResourceNodeState> statesByNode =>
        aggregateState.StatesByNode;
    private List<WorldResourceNode> nodeView => aggregateState.NodeView;
    private int initializedWorldRevision
    {
        get => aggregateState.InitializedWorldRevision;
        set => aggregateState.InitializedWorldRevision = value;
    }
    private int initializedDecorationVersion
    {
        get => aggregateState.InitializedDecorationVersion;
        set => aggregateState.InitializedDecorationVersion = value;
    }

    public WorldResourceRuntime(
        WorldResourceEnvironmentDependencies environment,
        WorldResourceInfrastructureDependencies infrastructure,
        WorldResourceProgressionDependencies progression,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        environment = environment ?? throw new ArgumentNullException(nameof(environment));
        infrastructure = infrastructure
            ?? throw new ArgumentNullException(nameof(infrastructure));
        progression = progression ?? throw new ArgumentNullException(nameof(progression));
        this.environment = environment.Environment;
        catalog = environment.Catalog;
        outputPort = environment.Output;
        nodeHosts = infrastructure.NodeHosts;
        random = progression.RandomStreamProvider.Get("economy:world-resources");
        persistentIds = progression.PersistentIds;
        research = progression.Research;
        grandProjectBenefits = progression.GrandProjectBenefits;
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public int Version
    {
        get => aggregateState.Version;
        private set => aggregateState.Version = value;
    }
    public int NodeCount => nodeView.Count;
    public IReadOnlyList<WorldResourceNode> Nodes => nodeView;

    public void Initialize()
    {
    }

    public void Tick()
    {
        if (!environment.TryCaptureTopology(
                out WorldResourceTopologySnapshot topology))
        {
            return;
        }

        if (initializedWorldRevision == topology.WorldRevision
            && initializedDecorationVersion == topology.StructureVersion)
        {
            return;
        }

        Rebuild(topology);
    }

    public void Dispose()
    {
        ClearNodes();
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

        WorldResourceRenewablePatchSnapshot patch = default;
        bool hasRenewablePatch = source.IsRenewablePatch
            && environment.TryGetRenewablePatch(
                source.PatchId,
                out patch);
        bool hasResource = source.IsRenewablePatch
            ? hasRenewablePatch
                && patch.CurrentResource >= PatchHarvestAmount
            : source.RemainingCycles > 0;
        bool researchUnlocked = IsResearchUnlocked(recipe, out string researchReason);
        bool outputAvailable = TryResolveOutputCapacity(
            recipe,
            nodeState.Position,
            out string outputReason);
        bool available = hasResource && researchUnlocked && outputAvailable;
        string reason = available
            ? string.Empty
            : !hasResource
                ? source.IsRenewablePatch
                    ? "채집할 자원이 다시 자라는 중"
                    : "자원이 고갈됨"
                : !researchUnlocked
                    ? researchReason
                    : outputReason;
        float resourceRatio = source.IsRenewablePatch
            ? hasRenewablePatch
                ? patch.ResourceRatio
                : 0f
            : Mathf.Clamp01(source.RemainingCycles);
        snapshot = new WorldResourceWorkSnapshot(
            nodeState.NodeId.Value,
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
            if (environment.ConsumeRenewablePatch(
                    source.PatchId,
                    PatchHarvestAmount) + 0.001f < PatchHarvestAmount)
            {
                source.CompletedWork = 0f;
                return false;
            }

            environment.RefreshRenewablePatch(source.PatchId);
        }
        else
        {
            source.RemainingCycles = Mathf.Max(0, source.RemainingCycles - 1);
            if (source.RemainingCycles == 0)
            {
                foreach (string visualId in source.VisualIds)
                {
                    environment.SetResourceVisualActive(visualId, false);
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
                    grandProjectBenefits.GetProductionOutputMultiplier(
                        recipe.FacilityTag);
                int outputAmount = Mathf.Max(
                    1,
                    Mathf.RoundToInt(output.Amount * multiplier));
                if (!outputPort.SpawnOutput(
                        output.ItemId,
                        outputAmount,
                        nodeState.Position))
                {
                    throw new InvalidOperationException(
                        $"World resource '{recipe.RecipeId}' failed to materialize "
                        + $"{outputAmount}x '{output.ItemId}' after output-capacity admission.");
                }
            }
        }

        source.CompletedWork = 0f;
        cycleCompleted = true;
        Version++;
        nodeHosts.MarkDynamicStateDirty();
        return true;
    }

    private bool TryResolveOutputCapacity(
        ProductionRecipeSO recipe,
        Vector2Int position,
        out string failureReason)
    {
        foreach (ProductionOutputDefinition output in recipe.Outputs
                     .Where(value => value != null && value.Probability > 0f)
                     .GroupBy(value => value.ItemId, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            int amount = Mathf.Max(1, Mathf.CeilToInt(output.Amount));
            if (!outputPort.CanSpawnOutput(
                    output.ItemId,
                    amount,
                    position,
                    out DomainFailure failure))
            {
                failureReason = failure.Code.ToString();
                return false;
            }
        }

        failureReason = string.Empty;
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
                    && statesById.TryGetValue(
                        (BuildingInstanceId)node.NodeId,
                        out state)));
    }

    public DungeonWorldResourceSaveData Capture()
    {
        DungeonWorldResourceSaveData data = new DungeonWorldResourceSaveData();
        foreach (WorldResourceNodeState state in statesById.Values
                     .OrderBy(entry => entry.NodeId.Value, StringComparer.Ordinal))
        {
            WorldResourceNodeSaveData node = new WorldResourceNodeSaveData
            {
                buildingInstanceId = state.NodeId.Value,
                gridX = state.Position.x,
                gridY = state.Position.y
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

    public WorldResourceRestoreCandidate BuildRestore(
        DungeonWorldResourceSaveData saveData)
    {
        RequireSaveRoot(saveData);
        WorldResourceAggregateState restored = new()
        {
            InitializedWorldRevision = -1,
            InitializedDecorationVersion = -1,
            RequireExactRebind = true,
            Version = aggregateState.Version + 1
        };
        restored.NodeView.AddRange(nodeView.Where(node => node != null));
        HashSet<BuildingInstanceId> ids = new();
        HashSet<Vector2Int> positions = new();
        foreach (WorldResourceNodeSaveData savedNode in saveData.nodes)
        {
            WorldResourceNodeState state = BuildRestoreNode(
                savedNode,
                ids,
                positions);
            restored.StatesById.Add(state.NodeId, state);
        }

        return new WorldResourceRestoreCandidate(restored);
    }

    public void Restore(WorldResourceRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }

    private void Rebuild(WorldResourceTopologySnapshot topology)
    {
        DungeonWorldResourceSaveData retained = Capture();
        bool requireExactRebind = aggregateState.RequireExactRebind;
        Dictionary<Vector2Int, WorldResourceNodeSaveData> retainedByPosition =
            retained.nodes.ToDictionary(
                saved => new Vector2Int(saved.gridX, saved.gridY));
        ClearNodes();
        initializedWorldRevision = topology.WorldRevision;
        initializedDecorationVersion = topology.StructureVersion;

        Dictionary<Vector2Int, List<WorldResourceVisualSnapshot>> visualsByCell =
            topology.Visuals
                .GroupBy(visual => visual.Position)
                .ToDictionary(group => group.Key, group => group.ToList());
        foreach (KeyValuePair<Vector2Int, List<WorldResourceVisualSnapshot>> entry in visualsByCell)
        {
            foreach (IGrouping<WorldResourceVisualKind, WorldResourceVisualSnapshot> kindGroup
                     in entry.Value.GroupBy(visual => visual.Kind))
            {
                WorkTypeId workTypeId = kindGroup.Key == WorldResourceVisualKind.Tree
                    ? BuiltInWorkTypeIds.Logging
                    : BuiltInWorkTypeIds.Quarry;
                string recipeId = kindGroup.Key == WorldResourceVisualKind.Tree
                    ? "source:logging"
                    : "source:saltstone";
                WorldResourceSourceState source = GetOrCreateSource(
                    entry.Key,
                    workTypeId,
                    recipeId,
                    renewablePatch: null,
                    retainedByPosition);
                source.RemainingCycles = Mathf.Max(1, kindGroup.Count());
                source.VisualIds.AddRange(kindGroup.Select(visual => visual.VisualId));
            }
        }

        foreach (WorldResourceRenewablePatchSnapshot patch in
                 topology.RenewablePatches)
        {
            GetOrCreateSource(
                patch.Position,
                BuiltInWorkTypeIds.Gather,
                "source:grass",
                patch,
                retainedByPosition);
        }

        if (retained.nodes.Count > 0)
        {
            if (requireExactRebind)
            {
                ApplyRestoreStrict(retained);
            }
            else
            {
                ApplyRetainedStateBestEffort(retained);
            }
        }
        aggregateState.RequireExactRebind = false;
        foreach (WorldResourceNodeState state in statesById.Values)
        {
            CreateNode(state);
            ApplyVisualState(state);
        }

        Version++;
        nodeHosts.ResetCandidatesAndReplan();
    }

    private WorldResourceSourceState GetOrCreateSource(
        Vector2Int position,
        WorkTypeId workTypeId,
        string recipeId,
        WorldResourceRenewablePatchSnapshot? renewablePatch,
        IReadOnlyDictionary<Vector2Int, WorldResourceNodeSaveData>
            retainedByPosition)
    {
        WorldResourceNodeState node = statesById.Values.FirstOrDefault(
            candidate => candidate.Position == position);
        if (node == null)
        {
            BuildingInstanceId nodeId = default;
            if (retainedByPosition != null
                && retainedByPosition.TryGetValue(
                    position,
                    out WorldResourceNodeSaveData retained))
            {
                nodeId = (BuildingInstanceId)retained.buildingInstanceId;
            }
            if (!nodeId.IsValid)
            {
                do
                {
                    nodeId = persistentIds.NewBuildingInstanceId();
                }
                while (statesById.ContainsKey(nodeId));
            }

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
                RemainingCycles = renewablePatch.HasValue ? -1 : 1,
                PatchId = renewablePatch.HasValue
                    ? renewablePatch.Value.PatchId
                    : default
            };
            node.Sources.Add(workTypeId, source);
        }

        return source;
    }

    private void CreateNode(WorldResourceNodeState state)
    {
        WorldResourceNode node = nodeHosts.CreateNode(
            this,
            state.NodeId,
            state.Position,
            ResolveNodeDisplayName(state));
        state.Node = node;
        statesByNode[node] = state;
        nodeView.Add(node);
    }

    private void ApplyRestoreStrict(DungeonWorldResourceSaveData saveData)
    {
        if (saveData.nodes.Count != statesById.Count)
        {
            throw new InvalidOperationException(
                "World-resource topology changed after restore preflight.");
        }

        foreach (WorldResourceNodeSaveData savedNode in saveData.nodes)
        {
            BuildingInstanceId nodeId =
                (BuildingInstanceId)savedNode.buildingInstanceId;
            if (!statesById.TryGetValue(nodeId, out WorldResourceNodeState state)
                || state.Position.x != savedNode.gridX
                || state.Position.y != savedNode.gridY
                || savedNode.sources.Count != state.Sources.Count)
            {
                throw new InvalidOperationException(
                    $"World-resource node '{savedNode.buildingInstanceId}' could not be rebound exactly.");
            }

            foreach (WorldResourceSourceSaveData savedSource in savedNode.sources)
            {
                WorkTypeId workTypeId = new(savedSource.workTypeId);
                if (!state.Sources.TryGetValue(
                        workTypeId,
                        out WorldResourceSourceState source)
                    || !string.Equals(
                        source.RecipeId,
                        savedSource.recipeId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"World-resource source '{savedSource.workTypeId}' no longer matches node '{nodeId.Value}'.");
                }

                source.CompletedWork = savedSource.completedWork;
                if (!source.IsRenewablePatch)
                {
                    if (savedSource.remainingCycles > source.VisualIds.Count)
                    {
                        throw new InvalidOperationException(
                            $"World-resource remaining cycles exceed authored visuals for '{nodeId.Value}'.");
                    }
                    source.RemainingCycles = savedSource.remainingCycles;
                }
            }
        }
    }

    private void ApplyRetainedStateBestEffort(
        DungeonWorldResourceSaveData saveData)
    {
        foreach (WorldResourceNodeSaveData savedNode in saveData.nodes)
        {
            BuildingInstanceId nodeId =
                (BuildingInstanceId)savedNode.buildingInstanceId;
            if (!statesById.TryGetValue(nodeId, out WorldResourceNodeState state))
            {
                continue;
            }
            foreach (WorldResourceSourceSaveData savedSource in savedNode.sources)
            {
                WorkTypeId workTypeId = new(savedSource.workTypeId);
                if (!state.Sources.TryGetValue(
                        workTypeId,
                        out WorldResourceSourceState source)
                    || !string.Equals(
                        source.RecipeId,
                        savedSource.recipeId,
                        StringComparison.Ordinal)
                    || !catalog.TryGetRecipe(
                        source.RecipeId,
                        out ProductionRecipeSO recipe))
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
                        source.VisualIds.Count);
                }
            }
        }
    }

    private static void RequireSaveRoot(DungeonWorldResourceSaveData saveData)
    {
        if (saveData == null)
        {
            throw new InvalidOperationException("World-resource payload is null.");
        }
        if (saveData.version != DungeonWorldResourceSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"World-resource payload version {saveData.version} is not current V{DungeonWorldResourceSaveData.CurrentVersion}.");
        }
        if (saveData.nodes == null || saveData.nodes.Count > 2048)
        {
            throw new InvalidOperationException(
                "World-resource payload must contain at most 2048 non-null nodes.");
        }
    }

    private WorldResourceNodeState BuildRestoreNode(
        WorldResourceNodeSaveData savedNode,
        ISet<BuildingInstanceId> ids,
        ISet<Vector2Int> positions)
    {
        if (savedNode == null || savedNode.sources == null
            || savedNode.sources.Count == 0 || savedNode.sources.Count > 8)
        {
            throw new InvalidOperationException(
                "World-resource payload contains a null node or invalid source count.");
        }
        BuildingInstanceId nodeId =
            (BuildingInstanceId)savedNode.buildingInstanceId;
        if (!nodeId.IsValid
            || !string.Equals(
                nodeId.Value,
                savedNode.buildingInstanceId,
                StringComparison.Ordinal)
            || !ids.Add(nodeId))
        {
            throw new InvalidOperationException(
                $"World-resource building instance ID '{savedNode.buildingInstanceId}' is invalid or duplicated.");
        }
        Vector2Int position = new(savedNode.gridX, savedNode.gridY);
        if (!positions.Add(position))
        {
            throw new InvalidOperationException(
                $"World-resource payload has ambiguous node position {position}.");
        }

        WorldResourceNodeState state = new()
        {
            NodeId = nodeId,
            Position = position
        };
        foreach (WorldResourceSourceSaveData savedSource in savedNode.sources)
        {
            WorldResourceSourceState source = BuildRestoreSource(savedSource);
            if (!state.Sources.TryAdd(source.WorkTypeId, source))
            {
                throw new InvalidOperationException(
                    $"World-resource source '{source.WorkTypeId.Value}' is duplicated on '{nodeId.Value}'.");
            }
        }

        return state;
    }

    private WorldResourceSourceState BuildRestoreSource(
        WorldResourceSourceSaveData savedSource)
    {
        if (savedSource == null)
        {
            throw new InvalidOperationException(
                "World-resource payload contains a null source.");
        }
        WorkTypeId workTypeId = new(savedSource.workTypeId);
        if (!workTypeId.IsValid
            || !string.Equals(
                workTypeId.Value,
                savedSource.workTypeId,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(savedSource.recipeId)
            || !string.Equals(
                savedSource.recipeId,
                savedSource.recipeId.Trim(),
                StringComparison.Ordinal)
            || !catalog.TryGetRecipe(
                savedSource.recipeId,
                out ProductionRecipeSO recipe)
            || recipe.WorkTypeId != workTypeId)
        {
            throw new InvalidOperationException(
                $"World-resource source '{savedSource?.workTypeId}' references an invalid work type or recipe.");
        }
        if (float.IsNaN(savedSource.completedWork)
            || float.IsInfinity(savedSource.completedWork)
            || savedSource.completedWork < 0f
            || savedSource.completedWork > recipe.RequiredWork)
        {
            throw new InvalidOperationException(
                $"World-resource progress {savedSource.completedWork} is outside [0, {recipe.RequiredWork}].");
        }
        bool renewable = string.Equals(
            savedSource.recipeId,
            "source:grass",
            StringComparison.Ordinal);
        if ((renewable && savedSource.remainingCycles != -1)
            || (!renewable
                && (savedSource.remainingCycles < 0
                    || savedSource.remainingCycles > 1024)))
        {
            throw new InvalidOperationException(
                $"World-resource remaining cycle count {savedSource.remainingCycles} is invalid.");
        }

        return new WorldResourceSourceState
        {
            WorkTypeId = workTypeId,
            RecipeId = recipe.RecipeId,
            CompletedWork = savedSource.completedWork,
            RemainingCycles = savedSource.remainingCycles
        };
    }

    private void ApplyVisualState(WorldResourceNodeState state)
    {
        foreach (WorldResourceSourceState source in state.Sources.Values)
        {
            if (source.IsRenewablePatch)
            {
                environment.RefreshRenewablePatch(source.PatchId);
                continue;
            }

            bool active = source.RemainingCycles > 0;
            foreach (string visualId in source.VisualIds)
            {
                environment.SetResourceVisualActive(visualId, active);
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

        if (!research.IsCompleted(recipe.RequiredResearchId))
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
                nodeHosts.DestroyNode(node);
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

}
