using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

internal sealed class WorldResourceSourceState
{
    public WorkTypeId WorkTypeId;
    public string RecipeId = string.Empty;
    public float CompletedWork;
    public int RemainingCycles;
    public int CompletedCycleSequence;
    public List<CraftContributionSaveData> WorkContributions = new();
    public WorldResourcePendingOutputSaveData PendingOutput = new();
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
        IWorldResourceSourceBindingCatalog sourceBindings,
        IWorldResourceOutputMaximumEnvelopeAuthority maximumOutputs,
        IWorldResourceOutputPublicationPort outputPublication,
        IDomainFactPublisher domainFacts,
        IGameClock gameClock)
    {
        Environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        SourceBindings = sourceBindings
            ?? throw new ArgumentNullException(nameof(sourceBindings));
        MaximumOutputs = maximumOutputs
            ?? throw new ArgumentNullException(nameof(maximumOutputs));
        OutputPublication = outputPublication
            ?? throw new ArgumentNullException(nameof(outputPublication));
        DomainFacts = domainFacts
            ?? throw new ArgumentNullException(nameof(domainFacts));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public IWorldResourceEnvironmentPort Environment { get; }
    public IResourceEconomyContentCatalog Catalog { get; }
    public IWorldResourceSourceBindingCatalog SourceBindings { get; }
    public IWorldResourceOutputMaximumEnvelopeAuthority MaximumOutputs { get; }
    public IWorldResourceOutputPublicationPort OutputPublication { get; }
    public IDomainFactPublisher DomainFacts { get; }
    public IGameClock GameClock { get; }
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
    private readonly IWorldResourceSourceBindingCatalog sourceBindings;
    private readonly IWorldResourceOutputMaximumEnvelopeAuthority maximumOutputs;
    private readonly IWorldResourceOutputPublicationPort outputPublication;
    private readonly IDomainFactPublisher domainFacts;
    public string LastFactFailure { get; private set; } = string.Empty;
    private readonly IGameClock gameClock;
    private readonly IWorldResourceResearchPort research;
    private readonly IWorldResourceNodeHostPort nodeHosts;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IRandomStreamProvider randomStreamProvider;
    private readonly CanonicalProductionOutputResolver outputResolver;
    private readonly IPersistentIdGenerator persistentIds;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private int activeOutputTransactions;
    private bool outputTransactionPoisoned;
    private readonly Dictionary<string, RetainedOutputTransaction>
        retainedOutputTransactions = new(StringComparer.Ordinal);

    private sealed class RetainedOutputTransaction
    {
        internal WorldResourceOutputPublicationTransaction Transaction;
        internal WorldResourceSourceDebit SourceDebit;
    }

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
        sourceBindings = environment.SourceBindings;
        maximumOutputs = environment.MaximumOutputs;
        outputPublication = environment.OutputPublication;
        domainFacts = environment.DomainFacts;
        gameClock = environment.GameClock;
        nodeHosts = infrastructure.NodeHosts;
        randomStreamProvider = progression.RandomStreamProvider;
        outputResolver = new CanonicalProductionOutputResolver(
            randomStreamProvider);
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
        RequireNoTransientOutputTransaction("dispose");
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
        bool pendingOutputReady = source.PendingOutput != null
            && !source.PendingOutput.IsEmpty;
        bool hasResource = pendingOutputReady || (source.IsRenewablePatch
            ? hasRenewablePatch
                && patch.CurrentResource >= PatchHarvestAmount
            : source.RemainingCycles > 0);
        bool researchUnlocked = IsResearchUnlocked(recipe, out string researchReason);
        bool available = pendingOutputReady || hasResource && researchUnlocked;
        string reason = available
            ? string.Empty
            : !hasResource
                ? source.IsRenewablePatch
                    ? "채집할 자원이 다시 자라는 중"
                    : "자원이 고갈됨"
                : researchReason;
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
            reason,
            pendingOutputReady);
        return true;
    }

    public bool TryFinalizePendingOutput(
        WorldResourceNode node,
        WorkTypeId workTypeId,
        out bool cycleCompleted)
    {
        cycleCompleted = false;
        if (!TryGetNodeState(node, out WorldResourceNodeState nodeState)
            || !nodeState.Sources.TryGetValue(
                workTypeId,
                out WorldResourceSourceState source)
            || source.PendingOutput == null
            || source.PendingOutput.IsEmpty
            || !catalog.TryGetRecipe(
                source.RecipeId,
                out ProductionRecipeSO recipe))
        {
            return false;
        }

        ValidatePendingOutput(nodeState.NodeId, source);
        if (!TryCommitFrozenCycle(nodeState, source))
            return false;

        cycleCompleted = true;
        Version++;
        nodeHosts.MarkDynamicStateDirty();
        return true;
    }

    public bool ApplyWork(
        WorldResourceNode node,
        WorkTypeId workTypeId,
        float amount,
        out bool cycleCompleted) => ApplyWork(
            node,
            workTypeId,
            amount,
            null,
            out cycleCompleted);

    public bool ApplyWork(
        WorldResourceNode node,
        WorkTypeId workTypeId,
        float amount,
        string contributorCharacterId,
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

        float beforeWork = source.CompletedWork;
        source.CompletedWork = Mathf.Min(
            recipe.RequiredWork,
            source.CompletedWork + Mathf.Max(0f, amount));
        float acceptedWork = Mathf.Max(0f, source.CompletedWork - beforeWork);
        if (acceptedWork > 0f)
        {
            string workerId = contributorCharacterId?.Trim() ?? string.Empty;
            CraftContributionAccumulator contributions = new(
                source.WorkContributions);
            contributions.Add(workerId, acceptedWork, 0f);
            source.WorkContributions = contributions.Capture();
        }
        Version++;
        if (source.CompletedWork + 0.001f < recipe.RequiredWork)
        {
            return true;
        }

        EnsureFrozenPendingOutput(nodeState, source, recipe);
        if (!TryCommitFrozenCycle(nodeState, source))
            return false;

        cycleCompleted = true;
        Version++;
        nodeHosts.MarkDynamicStateDirty();
        return true;
    }

    private void EnsureFrozenPendingOutput(
        WorldResourceNodeState node,
        WorldResourceSourceState source,
        ProductionRecipeSO recipe)
    {
        if (source.PendingOutput != null && !source.PendingOutput.IsEmpty)
        {
            ValidatePendingOutput(node.NodeId, source);
            return;
        }

        source.PendingOutput = CapturePendingOutput(node, source, recipe);
        Version++;
    }

    private WorldResourcePendingOutputSaveData CapturePendingOutput(
        WorldResourceNodeState node,
        WorldResourceSourceState source,
        ProductionRecipeSO recipe)
    {
        recipe.ValidateCanonicalOutputLinesOrThrow();
        ProductionOutputFactor factor = ProductionOutputFactorAuthority
            .ResolveCurrent(grandProjectBenefits, recipe.FacilityTag);
        ValidateFrozenFactorWithinMaximum(recipe, factor);
        return CapturePendingOutput(
            node,
            source,
            recipe,
            rootSeed: null,
            factor);
    }

    private WorldResourcePendingOutputSaveData CapturePendingOutput(
        WorldResourceNodeState node,
        WorldResourceSourceState source,
        ProductionRecipeSO recipe,
        int? rootSeed,
        ProductionOutputFactor factor)
    {
        int sequence = checked(source.CompletedCycleSequence + 1);
        string operationId = FormatCycleOperationId(
            node.NodeId,
            source.WorkTypeId,
            sequence);
        ProductionBillId syntheticBillId = (ProductionBillId)(
            "production-bill:" + operationId);
        CanonicalProductionOutputResolution resolution = rootSeed.HasValue
            ? outputResolver.Resolve(
                rootSeed.Value,
                syntheticBillId,
                sequence,
                recipe.RecipeId,
                recipe.CaptureCanonicalOutputs(),
                factor,
                ProductionProcessKind.WorkOnly,
                100f)
            : outputResolver.Resolve(
                syntheticBillId,
                sequence,
                recipe.RecipeId,
                recipe.CaptureCanonicalOutputs(),
                factor,
                ProductionProcessKind.WorkOnly,
                100f);
        string recipeDigest = ProductionRecipeSemanticDigest.Capture(recipe);
        List<WorldResourceResolvedOutputLineSaveData> lines = new();
        long totalMass = 0L;
        foreach (CanonicalProductionResolvedOutputLine line in resolution.Lines
                     .OrderBy(value => value.DeterministicOrdinal))
        {
            long unitMass = 0L;
            long resolvedMass = 0L;
            if (line.IsPhysical && line.ResolvedQuantity > 0)
            {
                unitMass = outputPublication.GetDefinitionUnitMassGrams(
                    line.ItemId);
                resolvedMass = checked(unitMass * line.ResolvedQuantity);
                totalMass = checked(totalMass + resolvedMass);
            }
            lines.Add(new WorldResourceResolvedOutputLineSaveData
            {
                deterministicOrdinal = line.DeterministicOrdinal,
                outputLineId = line.OutputLineId,
                role = line.Role,
                itemId = line.ItemId,
                authoredQuantity = line.AuthoredQuantity,
                inclusionProbability = line.InclusionProbability,
                included = line.Included,
                resolvedQuantity = line.ResolvedQuantity,
                unitMassGrams = unitMass,
                resolvedMassGrams = resolvedMass
            });
        }

        WorldResourceOutputMaximumEnvelopeSnapshot maximum = maximumOutputs
            .Require(recipe.RecipeId);
        ValidateResolvedOutputWithinMaximum(
            recipe,
            recipeDigest,
            lines,
            totalMass,
            maximum);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("world-resource-frozen-output@2");
        digest.Append(resolution.RootSeed);
        digest.Append(sequence);
        digest.Append(operationId);
        digest.Append(recipe.RecipeId);
        digest.Append(recipeDigest);
        digest.Append(maximum.MaximumOutputMassGrams);
        digest.Append(maximum.SourceDigest);
        digest.Append(resolution.OutputFactorNumerator);
        digest.Append(resolution.OutputFactorDenominator);
        digest.Append(totalMass);
        digest.Append(lines.Count);
        foreach (WorldResourceResolvedOutputLineSaveData line in lines)
        {
            digest.Append(line.deterministicOrdinal);
            digest.Append(line.outputLineId);
            digest.AppendEnum(line.role);
            digest.Append(line.itemId);
            digest.Append(line.authoredQuantity);
            digest.AppendFloat(line.inclusionProbability);
            digest.Append(line.included);
            digest.Append(line.resolvedQuantity);
            digest.Append(line.unitMassGrams);
            digest.Append(line.resolvedMassGrams);
        }

        return new WorldResourcePendingOutputSaveData
        {
            rootSeed = resolution.RootSeed,
            cycleSequence = sequence,
            operationId = operationId,
            recipeId = recipe.RecipeId,
            recipeSourceDigest = recipeDigest,
            maximumOutputMassGrams = maximum.MaximumOutputMassGrams,
            maximumOutputSourceDigest = maximum.SourceDigest,
            outputFactorNumerator = resolution.OutputFactorNumerator,
            outputFactorDenominator = resolution.OutputFactorDenominator,
            outcomeFingerprint = digest.ComputeSha256(),
            physicalOutputMassGrams = totalMass,
            lines = lines,
            contributions = (source.WorkContributions
                    ?? new List<CraftContributionSaveData>())
                .Select(value => value?.Clone())
                .ToList()
        };
    }

    private bool TryCommitFrozenCycle(
        WorldResourceNodeState node,
        WorldResourceSourceState source)
    {
        WorldResourcePendingOutputSaveData pending = source.PendingOutput;
        if (pending == null || pending.IsEmpty || pending.lines == null)
            throw new InvalidOperationException(
                "World-resource commit requires a frozen output owner.");
        if (pending.domainApplied)
        {
            if (!TryPublishCycleFact(node, source, pending))
                return false;
            FinalizeCommittedCycle(source, pending);
            return true;
        }
        bool hasPhysicalOutput = pending.lines.Any(value => value != null
            && ProductionOutputRoleRules.IsPhysical(value.role)
            && value.resolvedQuantity > 0);
        if (!hasPhysicalOutput)
        {
            if (!TryDebitSource(source, out WorldResourceSourceDebit noOutputDebit))
                return false;
            ApplyCommittedCycle(source, pending, noOutputDebit, default);
            if (!TryPublishCycleFact(node, source, pending))
                return false;
            FinalizeCommittedCycle(source, pending);
            return true;
        }

        if (retainedOutputTransactions.TryGetValue(
                pending.operationId,
                out RetainedOutputTransaction retained))
        {
            activeOutputTransactions++;
            try
            {
                WorldResourceOutputCommitStatus retainedStatus =
                    outputPublication.CommitReleased(
                        retained.Transaction,
                        node.Position,
                        out WorldResourceOutputCommitReceipt retainedReceipt,
                        out string retainedFailure);
                if (retainedStatus == WorldResourceOutputCommitStatus.Committed)
                {
                    retainedOutputTransactions.Remove(pending.operationId);
                    ApplyCommittedCycle(
                        source,
                        pending,
                        retained.SourceDebit,
                        retainedReceipt);
                    if (!TryPublishCycleFact(node, source, pending))
                        return false;
                    FinalizeCommittedCycle(source, pending);
                    return true;
                }
                if (retainedStatus
                    == WorldResourceOutputCommitStatus.RetryableRetained)
                {
                    return false;
                }
                if (retainedStatus
                    == WorldResourceOutputCommitStatus.RejectedAndRolledBack)
                {
                    retainedOutputTransactions.Remove(pending.operationId);
                    if (!TryRollbackSourceDebit(source, retained.SourceDebit))
                    {
                        outputTransactionPoisoned = true;
                        throw new InvalidOperationException(
                            "World-resource retained source rollback failed: "
                            + retainedFailure);
                    }
                    return false;
                }

                outputTransactionPoisoned = true;
                throw new InvalidOperationException(
                    "World-resource retained publication is poisoned: "
                    + retainedFailure);
            }
            finally
            {
                activeOutputTransactions--;
            }
        }

        if (!outputPublication.TryPrepare(
                pending,
                node.Position,
                out WorldResourceOutputPublicationTransaction transaction,
                out _))
        {
            return false;
        }

        activeOutputTransactions++;
        WorldResourceSourceDebit debit = default;
        try
        {
            if (!TryDebitSource(source, out debit))
            {
                RollbackPreparedOrThrow(
                    transaction,
                    prepared: true,
                    "world-resource-source-debit-unavailable");
                return false;
            }

            WorldResourceOutputCommitStatus status =
                outputPublication.CommitReleased(
                    transaction,
                    node.Position,
                    out WorldResourceOutputCommitReceipt receipt,
                    out string commitFailure);
            if (status == WorldResourceOutputCommitStatus.Committed)
            {
                ApplyCommittedCycle(source, pending, debit, receipt);
                if (!TryPublishCycleFact(node, source, pending))
                    return false;
                FinalizeCommittedCycle(source, pending);
                return true;
            }
            if (status == WorldResourceOutputCommitStatus.RejectedAndRolledBack)
            {
                if (!TryRollbackSourceDebit(source, debit))
                {
                    outputTransactionPoisoned = true;
                    throw new InvalidOperationException(
                        "World-resource source rollback failed after rejected publication: "
                        + commitFailure);
                }
                return false;
            }
            retainedOutputTransactions.Add(
                pending.operationId,
                new RetainedOutputTransaction
                {
                    Transaction = transaction,
                    SourceDebit = debit
                });
            if (status == WorldResourceOutputCommitStatus.RetryableRetained)
                return false;

            outputTransactionPoisoned = true;
            throw new InvalidOperationException(
                "World-resource publication transaction is poisoned: "
                + commitFailure);
        }
        finally
        {
            activeOutputTransactions--;
        }
    }

    private bool TryDebitSource(
        WorldResourceSourceState source,
        out WorldResourceSourceDebit debit)
    {
        debit = default;
        if (source.IsRenewablePatch)
        {
            if (!environment.TryConsumeRenewablePatchExact(
                    source.PatchId,
                    PatchHarvestAmount,
                    out WorldResourceRenewableDebitReceipt receipt))
            {
                return false;
            }
            debit = WorldResourceSourceDebit.Renewable(receipt);
            return true;
        }

        if (source.RemainingCycles <= 0)
            return false;
        int before = source.RemainingCycles;
        source.RemainingCycles = checked(before - 1);
        debit = WorldResourceSourceDebit.Finite(before);
        return true;
    }

    private bool TryRollbackSourceDebit(
        WorldResourceSourceState source,
        WorldResourceSourceDebit debit)
    {
        if (!debit.IsValid)
            return false;
        if (debit.IsRenewable)
        {
            return environment.TryRollbackRenewablePatchDebit(
                debit.RenewableReceipt);
        }
        if (source.RemainingCycles != debit.FiniteBefore - 1)
            return false;
        source.RemainingCycles = debit.FiniteBefore;
        return true;
    }

    private void ApplyCommittedCycle(
        WorldResourceSourceState source,
        WorldResourcePendingOutputSaveData pending,
        WorldResourceSourceDebit debit,
        in WorldResourceOutputCommitReceipt outputReceipt)
    {
        if (pending.domainApplied)
            return;
        if (!debit.IsValid)
            throw new InvalidOperationException(
                "World-resource terminal source debit is missing.");
        pending.renewableSource = debit.IsRenewable;
        if (debit.IsRenewable)
        {
            pending.sourceBefore = debit.RenewableReceipt.BeforeResource;
            pending.sourceAfter = debit.RenewableReceipt.AfterResource;
            pending.sourcePatchId = debit.RenewableReceipt.PatchId.Value;
        }
        else
        {
            pending.sourceBefore = debit.FiniteBefore;
            pending.sourceAfter = debit.FiniteBefore - 1;
            pending.sourcePatchId = string.Empty;
        }
        if (outputReceipt.IsValid)
        {
            if (outputReceipt.TotalMassGrams != pending.physicalOutputMassGrams)
                throw new InvalidOperationException(
                    "World-resource committed output mass drifted from the frozen owner.");
            pending.outputBatchCommitId = outputReceipt.BatchCommitId;
            pending.outputDestinationId = outputReceipt.DestinationId;
            pending.publishedStacks = outputReceipt.Stacks
                .Select(value => value?.Clone())
                .ToList();
        }
        pending.completedAbsoluteDay = Mathf.Max(
            1,
            Mathf.FloorToInt(gameClock.Time / 24f) + 1);
        pending.domainApplied = true;
        if (source.IsRenewablePatch)
        {
            environment.RefreshRenewablePatch(source.PatchId);
            return;
        }
        if (source.RemainingCycles != 0)
            return;
        foreach (string visualId in source.VisualIds)
            environment.SetResourceVisualActive(visualId, false);
    }

    private bool TryPublishCycleFact(
        WorldResourceNodeState node,
        WorldResourceSourceState source,
        WorldResourcePendingOutputSaveData pending)
    {
        LastFactFailure = string.Empty;
        if (!string.IsNullOrEmpty(pending.parentResultKey))
            return true;
        var fact = new WorldResourceCycleCompletedFact(node.NodeId, source.WorkTypeId, pending);
        DomainFactDelivery result;
        try
        {
            if (string.IsNullOrEmpty(pending.parentPublicationKey))
            {
                if (!domainFacts.TryReserve(fact, out var lease, out string failure))
                { LastFactFailure = failure; return false; }
                string key = lease.PublicationKey;
                if (string.IsNullOrWhiteSpace(key) || key != key.Trim() || key.Length > 512
                    || key.Any(char.IsControl))
                {
                    domainFacts.Cancel(lease);
                    LastFactFailure = "world-resource-fact-reservation-identity-missing";
                    return false;
                }
                // The physical/domain transition already happened. Save the opaque
                // identity before dispatch; any ambiguous result remains retryable.
                pending.parentPublicationKey = key;
                result = domainFacts.Publish(lease, fact);
            }
            else
            {
                if (domainFacts is not IDomainFactRecoveryPublisher recovery)
                { LastFactFailure = "world-resource-fact-recovery-unavailable"; return false; }
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.parentPublicationKey, fact) },
                        results, out string failure))
                { LastFactFailure = failure; return false; }
                result = results[0];
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            LastFactFailure = "world-resource-fact-dispatch-unconfirmed:" + exception.Message;
            return false;
        }
        if (result.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
            || result.OperationKey != pending.parentPublicationKey
            || string.IsNullOrEmpty(result.PublicationId) || result.Fingerprint?.Length != 64)
        {
            LastFactFailure = "world-resource-fact-delivery-unconfirmed:" + result.Detail;
            return false;
        }
        pending.parentResultKey = result.OperationKey;
        return true;
    }

    private void FinalizeCommittedCycle(
        WorldResourceSourceState source,
        WorldResourcePendingOutputSaveData pending)
    {
        if (!pending.domainApplied || string.IsNullOrEmpty(pending.parentResultKey))
            throw new InvalidOperationException(
                "World-resource cycle cannot retire before its parent outcome.");
        source.CompletedCycleSequence = pending.cycleSequence;
        source.PendingOutput = new WorldResourcePendingOutputSaveData();
        source.CompletedWork = 0f;
        source.WorkContributions.Clear();
    }

    private void RollbackPreparedOrThrow(
        WorldResourceOutputPublicationTransaction transaction,
        bool prepared,
        string reasonCode)
    {
        if (!prepared)
            return;
        if (outputPublication.TryRollback(
                transaction,
                reasonCode,
                out string rollbackFailure))
        {
            return;
        }
        outputTransactionPoisoned = true;
        throw new InvalidOperationException(
            "World-resource prepared output rollback failed: "
            + rollbackFailure);
    }

    private void RequireNoTransientOutputTransaction(string operation)
    {
        if (activeOutputTransactions != 0
            || retainedOutputTransactions.Count != 0
            || outputTransactionPoisoned)
        {
            throw new InvalidOperationException(
                "World-resource " + operation
                + " is blocked by a transient or poisoned output transaction.");
        }
    }

    internal static string FormatCycleOperationId(
        BuildingInstanceId nodeId,
        WorkTypeId workTypeId,
        int cycleSequence)
    {
        if (!nodeId.IsValid || !workTypeId.IsValid || cycleSequence <= 0)
            throw new ArgumentException("World-resource cycle identity is invalid.");
        return "world-resource:" + nodeId.Value + ":"
            + workTypeId.Value + ":"
            + cycleSequence.ToString("D8",
                System.Globalization.CultureInfo.InvariantCulture);
    }

    private readonly struct WorldResourceSourceDebit
    {
        private WorldResourceSourceDebit(
            bool isRenewable,
            int finiteBefore,
            WorldResourceRenewableDebitReceipt renewableReceipt)
        {
            IsRenewable = isRenewable;
            FiniteBefore = finiteBefore;
            RenewableReceipt = renewableReceipt;
        }

        internal bool IsRenewable { get; }
        internal int FiniteBefore { get; }
        internal WorldResourceRenewableDebitReceipt RenewableReceipt { get; }
        internal bool IsValid => IsRenewable
            ? RenewableReceipt.IsValid
            : FiniteBefore > 0;

        internal static WorldResourceSourceDebit Finite(int before) =>
            new(false, before, default);

        internal static WorldResourceSourceDebit Renewable(
            WorldResourceRenewableDebitReceipt receipt) =>
            new(true, 0, receipt);
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
        RequireNoTransientOutputTransaction("save-capture");
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
                    remainingCycles = source.RemainingCycles,
                    completedCycleSequence = source.CompletedCycleSequence,
                    workContributions = (source.WorkContributions
                            ?? new List<CraftContributionSaveData>())
                        .Select(value => value?.Clone())
                        .ToList(),
                    pendingOutput = source.PendingOutput.Clone()
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
        RequireNoTransientOutputTransaction("restore");
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }

    private void Rebuild(WorldResourceTopologySnapshot topology)
    {
        RequireNoTransientOutputTransaction("topology-rebuild");
        WorldResourceAggregateState previous = aggregateState;
        DungeonWorldResourceSaveData retained = Capture();
        WorldResourceAggregateState candidate = BuildTopologyCandidate(
            topology,
            retained,
            previous.RequireExactRebind,
            previous.Version);
        CommitTopologyCandidate(previous, candidate);
    }

    private WorldResourceAggregateState BuildTopologyCandidate(
        WorldResourceTopologySnapshot topology,
        DungeonWorldResourceSaveData retained,
        bool requireExactRebind,
        int previousVersion)
    {
        WorldResourceAggregateState candidate = new()
        {
            InitializedWorldRevision = topology.WorldRevision,
            InitializedDecorationVersion = topology.StructureVersion,
            RequireExactRebind = false,
            Version = checked(previousVersion + 1)
        };
        Dictionary<Vector2Int, WorldResourceNodeSaveData> retainedByPosition =
            retained.nodes.ToDictionary(
                saved => new Vector2Int(saved.gridX, saved.gridY));

        Dictionary<Vector2Int, List<WorldResourceVisualSnapshot>> visualsByCell =
            topology.Visuals
                .GroupBy(visual => visual.Position)
                .ToDictionary(group => group.Key, group => group.ToList());
        foreach (KeyValuePair<Vector2Int, List<WorldResourceVisualSnapshot>> entry in visualsByCell)
        {
            foreach (IGrouping<WorldResourceVisualKind, WorldResourceVisualSnapshot> kindGroup
                     in entry.Value.GroupBy(visual => visual.Kind))
            {
                WorldResourceSourceBinding binding = sourceBindings
                    .RequireVisual(kindGroup.Key);
                WorldResourceSourceState source = GetOrCreateSource(
                    candidate,
                    entry.Key,
                    binding.WorkTypeId,
                    binding.RecipeId,
                    renewablePatch: null,
                    retainedByPosition);
                source.RemainingCycles = Mathf.Max(1, kindGroup.Count());
                source.VisualIds.AddRange(kindGroup.Select(visual => visual.VisualId));
            }
        }

        foreach (WorldResourceRenewablePatchSnapshot patch in
                 topology.RenewablePatches)
        {
            WorldResourceSourceBinding binding = sourceBindings
                .RequireRenewablePatch(patch.HabitatType);
            GetOrCreateSource(
                candidate,
                patch.Position,
                binding.WorkTypeId,
                binding.RecipeId,
                patch,
                retainedByPosition);
        }

        if (retained.nodes.Count > 0)
        {
            if (requireExactRebind)
            {
                ApplyRestoreStrict(candidate, retained);
            }
            else
            {
                ApplyRetainedStateBestEffort(candidate, retained);
            }
        }

        foreach (WorldResourceNodeState state in candidate.StatesById.Values)
        {
            foreach (WorldResourceSourceState source in state.Sources.Values)
                ValidatePendingOutput(state.NodeId, source);
        }
        return candidate;
    }

    private void CommitTopologyCandidate(
        WorldResourceAggregateState previous,
        WorldResourceAggregateState candidate)
    {
        DestroyNodeHosts(previous);
        aggregateRootStore.Replace(candidate);
        try
        {
            PublishNodeHosts(candidate);
            nodeHosts.ResetCandidatesAndReplan();
        }
        catch (Exception commitFailure)
        {
            DestroyNodeHosts(candidate);
            aggregateRootStore.Replace(previous);
            try
            {
                PublishNodeHosts(previous);
                nodeHosts.ResetCandidatesAndReplan();
            }
            catch (Exception rollbackFailure)
            {
                throw new AggregateException(
                    "World-resource topology publication and rollback both failed.",
                    commitFailure,
                    rollbackFailure);
            }

            throw new InvalidOperationException(
                "World-resource topology publication failed and the previous state was restored.",
                commitFailure);
        }
    }

    private WorldResourceSourceState GetOrCreateSource(
        WorldResourceAggregateState target,
        Vector2Int position,
        WorkTypeId workTypeId,
        string recipeId,
        WorldResourceRenewablePatchSnapshot? renewablePatch,
        IReadOnlyDictionary<Vector2Int, WorldResourceNodeSaveData>
            retainedByPosition)
    {
        WorldResourceNodeState node = target.StatesById.Values.FirstOrDefault(
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
                while (target.StatesById.ContainsKey(nodeId));
            }

            node = new WorldResourceNodeState
            {
                NodeId = nodeId,
                Position = position
            };
            target.StatesById.Add(nodeId, node);
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

    private void PublishNodeHosts(WorldResourceAggregateState target)
    {
        foreach (WorldResourceNodeState state in target.StatesById.Values
                     .OrderBy(value => value.NodeId.Value, StringComparer.Ordinal))
        {
            CreateNode(target, state);
            ApplyVisualState(state);
        }
    }

    private void CreateNode(
        WorldResourceAggregateState target,
        WorldResourceNodeState state)
    {
        WorldResourceNode node = nodeHosts.CreateNode(
            this,
            state.NodeId,
            state.Position,
            ResolveNodeDisplayName(state));
        state.Node = node;
        target.StatesByNode[node] = state;
        target.NodeView.Add(node);
    }

    private void ApplyRestoreStrict(
        WorldResourceAggregateState target,
        DungeonWorldResourceSaveData saveData)
    {
        if (saveData.nodes.Count != target.StatesById.Count)
        {
            throw new InvalidOperationException(
                "World-resource topology changed after restore preflight.");
        }

        foreach (WorldResourceNodeSaveData savedNode in saveData.nodes)
        {
            BuildingInstanceId nodeId =
                (BuildingInstanceId)savedNode.buildingInstanceId;
            if (!target.StatesById.TryGetValue(
                    nodeId,
                    out WorldResourceNodeState state)
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
                source.CompletedCycleSequence =
                    savedSource.completedCycleSequence;
                source.WorkContributions = savedSource.workContributions
                    .Select(value => value.Clone())
                    .ToList();
                source.PendingOutput = savedSource.pendingOutput.Clone();
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
        WorldResourceAggregateState target,
        DungeonWorldResourceSaveData saveData)
    {
        foreach (WorldResourceNodeSaveData savedNode in saveData.nodes)
        {
            BuildingInstanceId nodeId =
                (BuildingInstanceId)savedNode.buildingInstanceId;
            if (!target.StatesById.TryGetValue(
                    nodeId,
                    out WorldResourceNodeState state))
            {
                WorldResourcePendingOutputSaveData orphaned = savedNode.sources?
                    .Select(value => value?.pendingOutput)
                    .FirstOrDefault(value => value != null && !value.IsEmpty);
                if (orphaned != null)
                {
                    throw new InvalidOperationException(
                        "World-resource topology orphaned a frozen output: "
                        + orphaned.operationId);
                }
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
                    if (savedSource.pendingOutput != null
                        && !savedSource.pendingOutput.IsEmpty)
                    {
                        throw new InvalidOperationException(
                            "World-resource topology orphaned a frozen output: "
                            + savedSource.pendingOutput.operationId);
                    }
                    continue;
                }

                source.CompletedWork = Mathf.Clamp(
                    savedSource.completedWork,
                    0f,
                    recipe.RequiredWork);
                source.CompletedCycleSequence =
                    savedSource.completedCycleSequence;
                source.WorkContributions = (savedSource.workContributions
                        ?? new List<CraftContributionSaveData>())
                    .Select(value => value?.Clone())
                    .ToList();
                source.PendingOutput = savedSource.pendingOutput.Clone();
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
            ValidatePendingOutput(state.NodeId, source);
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
        if (savedSource.completedCycleSequence < 0
            || savedSource.pendingOutput == null
            || savedSource.pendingOutput.parentPublicationKey == null
            || savedSource.pendingOutput.parentResultKey == null
            || savedSource.workContributions == null
            || savedSource.workContributions.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.characterId)
                || value.contributedWork <= 0f
                || !float.IsFinite(value.contributedWork)))
        {
            throw new InvalidOperationException(
                "World-resource cycle sequence or pending output is invalid.");
        }
        WorldResourceSourceBinding[] matchingBindings = sourceBindings.Bindings
            .Where(value => value.WorkTypeId == workTypeId
                && string.Equals(
                    value.RecipeId,
                    savedSource.recipeId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matchingBindings.Length == 0)
        {
            throw new InvalidOperationException(
                "World-resource source has no registered topology binding: "
                + savedSource.recipeId + "/" + workTypeId.Value);
        }
        bool renewable = matchingBindings.All(value =>
            value.Kind == WorldResourceSourceBindingKind.RenewablePatch);
        if (!renewable && matchingBindings.Any(value =>
                value.Kind == WorldResourceSourceBindingKind.RenewablePatch))
        {
            throw new InvalidOperationException(
                "World-resource source mixes finite and renewable bindings: "
                + savedSource.recipeId);
        }
        if ((renewable && savedSource.remainingCycles != -1)
            || (!renewable
                && (savedSource.remainingCycles < 0
                    || savedSource.remainingCycles > 1024)))
        {
            throw new InvalidOperationException(
                $"World-resource remaining cycle count {savedSource.remainingCycles} is invalid.");
        }
        if (savedSource.pendingOutput.domainApplied
            && savedSource.pendingOutput.renewableSource != renewable)
        {
            throw new InvalidOperationException(
                "World-resource terminal source kind does not match its registered binding.");
        }

        return new WorldResourceSourceState
        {
            WorkTypeId = workTypeId,
            RecipeId = recipe.RecipeId,
            CompletedWork = savedSource.completedWork,
            RemainingCycles = savedSource.remainingCycles,
            // Preserve the frozen debit identity during unbound preflight.
            // Exact topology rebind derives the live PatchId independently and
            // validates the same terminal evidence before publishing any nodes.
            PatchId = renewable && savedSource.pendingOutput.domainApplied
                ? new WildlifeHabitatPatchId(savedSource.pendingOutput.sourcePatchId)
                : default,
            CompletedCycleSequence = savedSource.completedCycleSequence,
            WorkContributions = savedSource.workContributions
                .Select(value => value.Clone())
                .ToList(),
            PendingOutput = savedSource.pendingOutput?.Clone()
                ?? throw new InvalidOperationException(
                    "World-resource pending output owner is missing.")
        };
    }

    private void ValidatePendingOutput(
        BuildingInstanceId nodeId,
        WorldResourceSourceState source)
    {
        if (!catalog.TryGetRecipe(
                source.RecipeId,
                out ProductionRecipeSO recipe))
        {
            throw new InvalidOperationException(
                "World-resource pending output recipe is missing: "
                + source.RecipeId);
        }
        bool completedWork = source.CompletedWork + 0.001f
            >= recipe.RequiredWork;
        if (source.PendingOutput == null || source.PendingOutput.IsEmpty)
        {
            if (completedWork)
            {
                throw new InvalidOperationException(
                    "World-resource completed work lacks a frozen output owner: "
                    + nodeId.Value + "/" + source.WorkTypeId.Value);
            }
            return;
        }
        if (!completedWork)
        {
            throw new InvalidOperationException(
                "World-resource frozen output lacks completed work: "
                + source.PendingOutput.operationId);
        }
        if (source.PendingOutput.rootSeed != randomStreamProvider.RootSeed)
        {
            throw new InvalidOperationException(
                "World-resource frozen output root seed drifted: "
                + source.PendingOutput.operationId);
        }

        WorldResourceNodeState node = new()
        {
            NodeId = nodeId
        };
        ProductionOutputFactor frozenFactor;
        try
        {
            frozenFactor = new ProductionOutputFactor(
                source.PendingOutput.outputFactorNumerator,
                source.PendingOutput.outputFactorDenominator);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException(
                "World-resource frozen output factor is invalid: "
                + source.PendingOutput.operationId,
                exception);
        }
        ValidateFrozenFactorWithinMaximum(recipe, frozenFactor);
        WorldResourcePendingOutputSaveData expected = CapturePendingOutput(
            node,
            source,
            recipe,
            source.PendingOutput.rootSeed,
            frozenFactor);
        if (!PendingOutputsEqual(source.PendingOutput, expected))
        {
            throw new InvalidOperationException(
                "World-resource frozen output authority drifted: "
                + source.PendingOutput.operationId);
        }
        ValidatePendingTerminal(source);
    }

    private static void ValidatePendingTerminal(WorldResourceSourceState source)
    {
        WorldResourcePendingOutputSaveData pending = source.PendingOutput;
        CraftContributionSaveData[] contributors = (pending.contributions
                ?? new List<CraftContributionSaveData>())
            .OrderBy(value => value?.characterId, StringComparer.Ordinal)
            .ToArray();
        CraftContributionSaveData[] sourceContributors = (source.WorkContributions
                ?? new List<CraftContributionSaveData>())
            .OrderBy(value => value?.characterId, StringComparer.Ordinal)
            .ToArray();
        if (contributors.Length != sourceContributors.Length
            || contributors.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.characterId)
                || value.contributedWork <= 0f
                || !float.IsFinite(value.contributedWork))
            || contributors.Where((value, index) =>
                    !string.Equals(value.characterId,
                        sourceContributors[index]?.characterId,
                        StringComparison.Ordinal)
                    || value.contributedWork
                        != sourceContributors[index]?.contributedWork)
                .Any())
        {
            throw new InvalidOperationException(
                "World-resource pending contributor provenance is invalid.");
        }
        if (!pending.domainApplied)
        {
            if (pending.renewableSource
                || pending.sourceBefore != 0f
                || pending.sourceAfter != 0f
                || !string.IsNullOrEmpty(pending.sourcePatchId)
                || !string.IsNullOrEmpty(pending.outputBatchCommitId)
                || !string.IsNullOrEmpty(pending.outputDestinationId)
                || (pending.publishedStacks?.Count ?? 0) != 0
                || pending.completedAbsoluteDay != 0
                || !string.IsNullOrEmpty(pending.parentPublicationKey)
                || !string.IsNullOrEmpty(pending.parentResultKey))
            {
                throw new InvalidOperationException(
                    "World-resource non-terminal owner contains terminal evidence.");
            }
            return;
        }
        if (pending.parentPublicationKey == null
            || pending.parentPublicationKey != pending.parentPublicationKey.Trim()
            || pending.parentPublicationKey.Length > 512
            || pending.parentPublicationKey.Any(char.IsControl)
            || !string.IsNullOrEmpty(pending.parentResultKey)
                && pending.parentResultKey != pending.parentPublicationKey)
            throw new InvalidOperationException("World-resource pending fact identity is invalid.");
        if (pending.completedAbsoluteDay < 1
            || pending.sourceBefore <= pending.sourceAfter
            || pending.sourceAfter < 0f
            || pending.renewableSource != source.IsRenewablePatch
            || pending.renewableSource
                && !string.Equals(pending.sourcePatchId, source.PatchId.Value,
                    StringComparison.Ordinal)
            || !pending.renewableSource
                && !string.IsNullOrEmpty(pending.sourcePatchId))
        {
            throw new InvalidOperationException(
                "World-resource terminal source evidence is invalid.");
        }
        bool physical = pending.physicalOutputMassGrams > 0L;
        long publishedMass = (pending.publishedStacks
                ?? new List<WorldResourcePublishedStackSaveData>())
            .Where(value => value != null)
            .Sum(value => value.massGrams);
        if (physical != !string.IsNullOrEmpty(pending.outputBatchCommitId)
            || physical != !string.IsNullOrEmpty(pending.outputDestinationId)
            || physical != (pending.publishedStacks?.Count > 0)
            || publishedMass != pending.physicalOutputMassGrams)
        {
            throw new InvalidOperationException(
                "World-resource terminal output receipt is invalid.");
        }
        if (!physical)
            return;

        WorldResourcePublishedStackSaveData[] stacks = pending.publishedStacks
            .ToArray();
        if (stacks.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.stackId)
                || string.IsNullOrWhiteSpace(value.outputLineId)
                || string.IsNullOrWhiteSpace(value.itemId)
                || value.quantity <= 0
                || value.massGrams <= 0L)
            || stacks.Select(value => value.stackId)
                .Distinct(StringComparer.Ordinal).Count() != stacks.Length)
        {
            throw new InvalidOperationException(
                "World-resource published stack provenance is invalid.");
        }

        WorldResourceResolvedOutputLineSaveData[] physicalLines =
            (pending.lines ?? new List<WorldResourceResolvedOutputLineSaveData>())
            .Where(value => value != null
                && ProductionOutputRoleRules.IsPhysical(value.role)
                && value.resolvedQuantity > 0)
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToArray();
        IGrouping<string, WorldResourcePublishedStackSaveData>[] byLine = stacks
            .GroupBy(value => value.outputLineId, StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        if (byLine.Length != physicalLines.Length)
        {
            throw new InvalidOperationException(
                "World-resource published stacks drifted from resolved output lines.");
        }
        for (int index = 0; index < physicalLines.Length; index++)
        {
            WorldResourceResolvedOutputLineSaveData line = physicalLines[index];
            IGrouping<string, WorldResourcePublishedStackSaveData> group =
                byLine[index];
            if (!string.Equals(group.Key, line.outputLineId, StringComparison.Ordinal)
                || group.Any(value => !string.Equals(
                    value.itemId,
                    line.itemId,
                    StringComparison.Ordinal))
                || group.Sum(value => value.quantity) != line.resolvedQuantity
                || group.Sum(value => value.massGrams) != line.resolvedMassGrams)
            {
                throw new InvalidOperationException(
                    "World-resource published stack totals drifted from resolved line: "
                    + line.outputLineId);
            }
        }
    }

    private static void ValidateFrozenFactorWithinMaximum(
        ProductionRecipeSO recipe,
        ProductionOutputFactor factor)
    {
        ProductionOutputFactor maximum = ProductionOutputFactorAuthority
            .ResolveMaximumGrandProject(recipe.FacilityTag);
        decimal resolved = checked(
            (decimal)factor.Numerator / factor.Denominator);
        decimal upper = checked(
            (decimal)maximum.Numerator / maximum.Denominator);
        if (resolved > upper)
        {
            throw new InvalidOperationException(
                "World-resource frozen output factor exceeds the authored maximum: "
                + recipe.RecipeId + ":" + factor + ">" + maximum);
        }
    }

    private static void ValidateResolvedOutputWithinMaximum(
        ProductionRecipeSO recipe,
        string recipeDigest,
        IReadOnlyList<WorldResourceResolvedOutputLineSaveData> actualLines,
        long actualMassGrams,
        WorldResourceOutputMaximumEnvelopeSnapshot maximum)
    {
        if (recipe == null
            || maximum == null
            || !string.Equals(
                maximum.RecipeId,
                recipe.RecipeId,
                StringComparison.Ordinal)
            || !string.Equals(
                maximum.RecipeSourceDigest,
                recipeDigest,
                StringComparison.Ordinal)
            || actualLines == null
            || actualLines.Count != maximum.Lines.Count
            || actualMassGrams < 0L
            || actualMassGrams > maximum.MaximumOutputMassGrams)
        {
            throw new InvalidOperationException(
                "World-resource frozen output exceeds or drifts from its maximum proof: "
                + (recipe?.RecipeId ?? string.Empty));
        }

        IReadOnlyDictionary<string, WorldResourceOutputMaximumLineSnapshot>
            maximumByLine = maximum.Lines.ToDictionary(
                value => value.OutputLineId,
                value => value,
                StringComparer.Ordinal);
        long recomputedMass = 0L;
        foreach (WorldResourceResolvedOutputLineSaveData actual in actualLines)
        {
            if (actual == null
                || !maximumByLine.TryGetValue(
                    actual.outputLineId,
                    out WorldResourceOutputMaximumLineSnapshot line)
                || actual.role != line.Role
                || !string.Equals(actual.itemId, line.ItemId,
                    StringComparison.Ordinal)
                || actual.inclusionProbability != line.InclusionProbability
                || actual.resolvedQuantity < 0
                || actual.resolvedMassGrams < 0L
                || actual.resolvedMassGrams > line.MaximumMassGrams
                || line.MaximumQuantity > 0
                    && actual.resolvedQuantity > line.MaximumQuantity
                || actual.resolvedQuantity > 0
                    && ProductionOutputRoleRules.IsPhysical(actual.role)
                    && (actual.unitMassGrams != line.UnitMassGrams
                        || actual.resolvedMassGrams != checked(
                            line.UnitMassGrams * actual.resolvedQuantity))
                || line.MaximumQuantity == 0
                    && actual.resolvedMassGrams != 0L)
            {
                throw new InvalidOperationException(
                    "World-resource frozen output line exceeds or drifts from its "
                    + "maximum proof: "
                    + recipe.RecipeId + "/"
                    + (actual?.outputLineId ?? string.Empty));
            }
            recomputedMass = checked(
                recomputedMass + actual.resolvedMassGrams);
        }
        if (recomputedMass != actualMassGrams)
        {
            throw new InvalidOperationException(
                "World-resource frozen output aggregate mass drifted from its lines: "
                + recipe.RecipeId);
        }
    }

    private static bool PendingOutputsEqual(
        WorldResourcePendingOutputSaveData left,
        WorldResourcePendingOutputSaveData right)
    {
        if (left == null || right == null
            || left.rootSeed != right.rootSeed
            || left.cycleSequence != right.cycleSequence
            || !string.Equals(left.operationId, right.operationId,
                StringComparison.Ordinal)
            || !string.Equals(left.recipeId, right.recipeId,
                StringComparison.Ordinal)
            || !string.Equals(left.recipeSourceDigest, right.recipeSourceDigest,
                StringComparison.Ordinal)
            || left.maximumOutputMassGrams != right.maximumOutputMassGrams
            || !string.Equals(
                left.maximumOutputSourceDigest,
                right.maximumOutputSourceDigest,
                StringComparison.Ordinal)
            || left.outputFactorNumerator != right.outputFactorNumerator
            || left.outputFactorDenominator != right.outputFactorDenominator
            || !string.Equals(left.outcomeFingerprint, right.outcomeFingerprint,
                StringComparison.Ordinal)
            || left.physicalOutputMassGrams != right.physicalOutputMassGrams
            || left.lines == null
            || right.lines == null
            || left.lines.Count != right.lines.Count)
        {
            return false;
        }

        for (int index = 0; index < left.lines.Count; index++)
        {
            WorldResourceResolvedOutputLineSaveData a = left.lines[index];
            WorldResourceResolvedOutputLineSaveData b = right.lines[index];
            if (a == null || b == null
                || a.deterministicOrdinal != b.deterministicOrdinal
                || !string.Equals(a.outputLineId, b.outputLineId,
                    StringComparison.Ordinal)
                || a.role != b.role
                || !string.Equals(a.itemId, b.itemId,
                    StringComparison.Ordinal)
                || a.authoredQuantity != b.authoredQuantity
                || a.inclusionProbability != b.inclusionProbability
                || a.included != b.included
                || a.resolvedQuantity != b.resolvedQuantity
                || a.unitMassGrams != b.unitMassGrams
                || a.resolvedMassGrams != b.resolvedMassGrams)
            {
                return false;
            }
        }
        return true;
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
        WorldResourceAggregateState current = aggregateState;
        DestroyNodeHosts(current);
        current.StatesById.Clear();
    }

    private void DestroyNodeHosts(WorldResourceAggregateState target)
    {
        foreach (WorldResourceNode node in target.NodeView.ToArray())
        {
            if (node != null)
            {
                nodeHosts.DestroyNode(node);
            }
        }

        target.NodeView.Clear();
        target.StatesByNode.Clear();
        foreach (WorldResourceNodeState state in target.StatesById.Values)
            state.Node = null;
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
