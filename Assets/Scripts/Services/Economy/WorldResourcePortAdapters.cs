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

    public bool TryConsumeRenewablePatchExact(
        WildlifeHabitatPatchId patchId,
        float amount,
        out WorldResourceRenewableDebitReceipt receipt)
    {
        receipt = default;
        WildlifeHabitatPatch patch = FindPatch(patchId);
        if (patch == null
            || float.IsNaN(amount)
            || float.IsInfinity(amount)
            || amount <= 0f
            || patch.CurrentResource < amount)
        {
            return false;
        }

        float before = patch.CurrentResource;
        float consumed = patch.Consume(amount);
        if (consumed != amount)
        {
            patch.SynchronizeResource(patch.ResourceCapacity, before);
            return false;
        }

        receipt = new WorldResourceRenewableDebitReceipt(
            patchId,
            amount,
            before,
            patch.CurrentResource);
        return receipt.IsValid;
    }

    public bool TryRollbackRenewablePatchDebit(
        WorldResourceRenewableDebitReceipt receipt)
    {
        WildlifeHabitatPatch patch = FindPatch(receipt.PatchId);
        if (patch == null
            || !receipt.IsValid
            || patch.CurrentResource != receipt.AfterResource
            || receipt.BeforeResource > patch.ResourceCapacity)
        {
            return false;
        }

        patch.SynchronizeResource(
            patch.ResourceCapacity,
            receipt.BeforeResource);
        return patch.CurrentResource == receipt.BeforeResource;
    }

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
        patch.HabitatType,
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
        node.Configure(runtime, nodeId, displayName);
        host.Bind(node);
        // Initialization publishes the host to the grid/world registries.
        // Bind the complete resource authority first so observers can never
        // misclassify a transient unbound Quarry host as a production bill
        // facility.
        host.Initialization(buildingArchetypes.WorldResourceNode, position);
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

public sealed class WorldResourceOutputPublicationPortAdapter :
    IWorldResourceOutputPublicationPort
{
    private const string AdmissionCommittedRollbackFailure =
        "physical-exact-source-committed-cannot-rollback";
    private sealed class PublicationToken :
        IWorldResourceOutputPublicationToken
    {
        internal PublicationToken(
            PhysicalItemExactSourcePublicationTransaction transaction)
        {
            Transaction = transaction;
        }

        internal PhysicalItemExactSourcePublicationTransaction Transaction
        {
            get;
        }
    }

    public const string OwnerDomain = "economy.world-resource-output";
    private const string ReleaseReason = "world-resource-output-completed";

    private readonly IPhysicalItemExactSourcePublicationService exactSources;
    private readonly IPhysicalItemMassQuery massQuery;

    public WorldResourceOutputPublicationPortAdapter(
        IPhysicalItemExactSourcePublicationService exactSources,
        IPhysicalItemMassQuery massQuery)
    {
        this.exactSources = exactSources
            ?? throw new ArgumentNullException(nameof(exactSources));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
    }

    public long GetDefinitionUnitMassGrams(string itemId) =>
        massQuery.GetDefinitionUnitMass((ItemDefinitionId)itemId).Value;

    public bool TryPrepare(
        WorldResourcePendingOutputSaveData pending,
        Vector2Int position,
        out WorldResourceOutputPublicationTransaction transaction,
        out string failureReason)
    {
        transaction = null;
        failureReason = string.Empty;
        if (pending == null
            || pending.IsEmpty
            || string.IsNullOrWhiteSpace(pending.operationId)
            || pending.lines == null)
        {
            failureReason = "world-resource-output-pending-invalid";
            return false;
        }

        try
        {
            FacilityBufferPlannedOutputSlice[] slices = pending.lines
                .Where(value => value != null
                    && ProductionOutputRoleRules.IsPhysical(value.role)
                    && value.resolvedQuantity > 0)
                .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                .Select(value =>
                {
                    ItemDefinitionId itemId = (ItemDefinitionId)value.itemId;
                    long unitMass = massQuery.GetDefinitionUnitMass(itemId).Value;
                    long lineMass = checked(unitMass * value.resolvedQuantity);
                    if (unitMass != value.unitMassGrams
                        || lineMass != value.resolvedMassGrams)
                    {
                        throw new InvalidOperationException(
                            "World-resource frozen output mass drifted: "
                            + value.outputLineId);
                    }
                    return new FacilityBufferPlannedOutputSlice(
                        value.outputLineId,
                        PhysicalItemMassSubject.ForDefinition(itemId),
                        value.resolvedQuantity);
                })
                .ToArray();
            if (slices.Length == 0)
            {
                failureReason = "world-resource-output-physical-vector-empty";
                return false;
            }

            PhysicalItemExactSourcePublicationPlan plan = new(
                OwnerDomain,
                pending.operationId,
                position,
                slices);
            if (!exactSources.TryPrepare(
                    plan,
                    out PhysicalItemExactSourcePublicationTransaction prepared,
                    out failureReason))
            {
                return false;
            }

            transaction = new WorldResourceOutputPublicationTransaction(
                new PublicationToken(prepared));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "world-resource-output-prepare:" + exception.Message;
            return false;
        }
    }

    public WorldResourceOutputCommitStatus CommitReleased(
        WorldResourceOutputPublicationTransaction transaction,
        Vector2Int position,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryUnwrap(transaction, out var prepared))
        {
            failureReason = "world-resource-output-transaction-invalid";
            return WorldResourceOutputCommitStatus.Poisoned;
        }
        if (exactSources.TryCommitReleased(
                prepared,
                position,
                ReleaseReason,
                out _,
                out failureReason))
        {
            return WorldResourceOutputCommitStatus.Committed;
        }

        string commitFailure = failureReason;
        if (exactSources.TryRollback(
                prepared,
                "world-resource-publication-rejected",
                out string rollbackFailure))
        {
            failureReason = commitFailure;
            return WorldResourceOutputCommitStatus.RejectedAndRolledBack;
        }
        if (string.Equals(
                rollbackFailure,
                AdmissionCommittedRollbackFailure,
                StringComparison.Ordinal))
        {
            failureReason = commitFailure;
            return WorldResourceOutputCommitStatus.RetryableRetained;
        }

        failureReason = commitFailure + ":rollback=" + rollbackFailure;
        return WorldResourceOutputCommitStatus.Poisoned;
    }

    public bool TryRollback(
        WorldResourceOutputPublicationTransaction transaction,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryUnwrap(transaction, out var prepared))
        {
            failureReason = "world-resource-output-transaction-invalid";
            return false;
        }
        return exactSources.TryRollback(
            prepared,
            reasonCode,
            out failureReason);
    }

    private static bool TryUnwrap(
        WorldResourceOutputPublicationTransaction transaction,
        out PhysicalItemExactSourcePublicationTransaction prepared)
    {
        if (transaction?.Token is PublicationToken token
            && token.Transaction.IsPrepared)
        {
            prepared = token.Transaction;
            return true;
        }
        prepared = default;
        return false;
    }
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
