using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class WorkOrderRestoreCandidate :
    IDungeonDiscardableRestoreCandidate
{
    private List<WorkOrderConstructionSiteRestoreCandidate> sites;

    internal WorkOrderRestoreCandidate(
        WorkOrderAggregateState state,
        List<WorkOrderConstructionSiteRestoreCandidate> sites)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        this.sites = sites ?? throw new ArgumentNullException(nameof(sites));
    }

    internal WorkOrderAggregateState State { get; }

    internal List<WorkOrderConstructionSiteRestoreCandidate> TakeSites()
    {
        List<WorkOrderConstructionSiteRestoreCandidate> result = sites
            ?? throw new InvalidOperationException(
                "Work-order restore candidate ownership was already transferred or discarded.");
        sites = null;
        return result;
    }

    public void Discard()
    {
        WorkOrderRuntime.DiscardSiteCandidates(sites);
        sites = null;
    }
}

internal sealed class WorkOrderConstructionSiteRestoreCandidate
{
    internal WorkOrderConstructionSiteRestoreCandidate(
        string orderId,
        ConstructionSite site)
    {
        OrderId = orderId
            ?? throw new ArgumentNullException(nameof(orderId));
        Site = site ?? throw new ArgumentNullException(nameof(site));
    }

    internal string OrderId { get; }
    internal ConstructionSite Site { get; }
}

public sealed partial class WorkOrderRuntime :
    IWorkOrderRuntime,
    IRepairWorkOutcomeRuntime,
    IWorkAccidentOperationIdAuthority,
    IWorkOrderQuery,
    IWorkOrderWorkerPolicyQuery,
    IWorkOrderWorkerPolicyCommand,
    IQualityTargetPipelineQuery,
    IQualityTargetPipelineCommand,
    IConstructionProjectWorkforceRuntime,
    IWorkOrderDestructiveDrainRetentionQuery,
    ITickable,
    IDungeonRestoreTransactionParticipant
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("WorkOrderRuntime.Tick");

    public const string ConstructionDestinationPrefix =
        WorkConstructionInputOwnerAuthority.DestinationPrefix;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IBuildingDefinitionLookup buildingDefinitionLookup;
    private readonly IObjectResolver objectResolver;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IGameClock gameClock;
    private readonly IUiClock uiClock;
    private readonly WorkOrderAggregateStateStore stateStore;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly ICraftQualityResolver qualityResolver;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly IWorkerNarrativeQualificationQuery narrativeQualifications;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBalanceWorkCalculator balanceWorkCalculator;
    private readonly IMaterialSalvageCalculator salvageCalculator;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly ICharacterPerformanceQuery performance;
    private readonly IProjectWorkforceRuntime projectWorkforce;
    private readonly IPhysicalItemSourcePublicationService materialSources;
    private readonly WorkOrderMaterialSupplyRuntime materialSupply;
    private IDomainFactPublisher constructionFacts;
    private readonly IDungeonRunFlowRuntime runFlow;
    private readonly Func<IProductionFacilityDestructiveDrainJournalQuery>
        destructiveDrainJournal;
    private GridBuildingPlacementService placementService;
    private readonly Dictionary<ConstructionSite, string> orderIdBySite =
        new Dictionary<ConstructionSite, string>();
    private List<WorkOrderConstructionSiteRestoreCandidate> stagedSiteCandidates;
    private PublishedWorkOrderSites publishedSites;
    private bool restoreTransactionActive;
    private bool restoreCandidatePrepared;
    private float nextReadyConstructionReplanAt;
    private readonly Dictionary<string, PendingDismantleObservation>
        pendingDismantleObservations = new(StringComparer.Ordinal);
    private string destructiveDrainJoinConflict = string.Empty;

    private sealed class PendingDismantleObservation
    {
        internal PendingDismantleObservation(
            string orderId,
            BuildableObject building,
            ProductionFacilityDestructiveDrainOperationId operationId,
            Action handler)
        {
            OrderId = orderId;
            Building = building;
            OperationId = operationId;
            Handler = handler;
        }

        internal string OrderId { get; }
        internal BuildableObject Building { get; }
        internal ProductionFacilityDestructiveDrainOperationId OperationId { get; }
        internal Action Handler { get; }
    }

    public string ParticipantId => "150.world.construction-sites";
    public int WorkOrderCandidateVersion => CurrentState.CandidateVersion;

    public bool TryCaptureRetention(
        ProductionFacilityDestructiveDrainOperationId operationId,
        out WorkOrderDestructiveDrainRetentionSnapshot snapshot,
        out string failureReason)
    {
        snapshot = default;
        failureReason = string.Empty;
        if (!operationId.IsValid)
        {
            failureReason =
                "work-order-destructive-drain-retention-operation-invalid";
            return false;
        }

        string[] owners = ordersById.Values
            .Where(value => value != null
                && value.workTypeId == BuiltInWorkTypeIds.Dismantle
                && string.Equals(
                    value.destructiveDrainOperationId,
                    operationId.Value,
                    StringComparison.Ordinal))
            .Select(value => value.workOrderId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (owners.Any(string.IsNullOrEmpty)
            || owners.Distinct(StringComparer.Ordinal).Count() != owners.Length)
        {
            failureReason =
                "work-order-destructive-drain-retention-owner-invalid";
            return false;
        }
        if (owners.Length > 1)
        {
            failureReason =
                "work-order-destructive-drain-retention-owner-duplicated";
            return false;
        }

        int version = WorkOrderCandidateVersion;
        string semantic =
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                string.Join("\n", new[]
                {
                    "work-order-destructive-drain-retention:v1",
                    version.ToString(CultureInfo.InvariantCulture),
                    operationId.Value,
                    string.Join("\n", owners)
                }));
        snapshot = new WorkOrderDestructiveDrainRetentionSnapshot(
            version,
            operationId.Value,
            Array.AsReadOnly(owners),
            semantic);
        return true;
    }
    public int Version => CurrentState.CandidateVersion;
    internal string LastTerminalOutcomeFailureForDebug { get; private set; } =
        string.Empty;
    public IReadOnlyList<WorkOrderProgressState> ActiveOrders => ordersById.Values
        .Where(order => order != null
            && order.status != WorkOrderStatus.Completed
            && order.status != WorkOrderStatus.Cancelled)
        .OrderBy(order => order.workOrderId, StringComparer.Ordinal)
        .Select(ToProgressState)
        .ToArray();
    public IReadOnlyList<QualityTargetPipelineSaveData> QualityPipelines =>
        CurrentState.QualityPipelinesById.Values
            .OrderBy(value => value.pipelineId, StringComparer.Ordinal)
            .Select(value => value.CloneNormalized())
            .ToArray();

    public WorkOrderRuntime(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IBuildingDefinitionLookup buildingDefinitionLookup,
        IObjectResolver objectResolver,
        WorkOrderExecutionServices executionServices,
        WorkOrderAggregateStateStore stateStore,
        IPhysicalItemSourcePublicationService materialSources,
        IWorkConstructionInputOwnerRuntime constructionInputs = null)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.buildingDefinitionLookup = buildingDefinitionLookup ?? throw new ArgumentNullException(nameof(buildingDefinitionLookup));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        WorkOrderExecutionServices execution = executionServices
            ?? throw new ArgumentNullException(nameof(executionServices));
        workforceReplanService = execution.Workforce;
        gameClock = execution.GameClock;
        uiClock = execution.UiClock;
        debugRules = execution.DebugRules;
        qualityResolver = ResolveOptional<ICraftQualityResolver>()
            ?? new DeterministicCraftQualityResolver();
        runSeedProvider = ResolveOptional<IRunSeedProvider>();
        narrativeQualifications =
            ResolveOptional<IWorkerNarrativeQualificationQuery>();
        characterWorld = ResolveOptional<ICharacterWorldQuery>();
        balanceWorkCalculator = ResolveOptional<IBalanceWorkCalculator>();
        salvageCalculator = ResolveOptional<IMaterialSalvageCalculator>();
        facilityCandidateCache = ResolveOptional<IFacilityCandidateCache>();
        performance = ResolveOptional<ICharacterPerformanceQuery>();
        projectWorkforce = ResolveOptional<IProjectWorkforceRuntime>();
        destructiveDrainJournal = () =>
            ResolveOptional<IProductionFacilityDestructiveDrainJournalQuery>();
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.materialSources = materialSources
            ?? throw new ArgumentNullException(nameof(materialSources));
        runFlow = ResolveOptional<IDungeonRunFlowRuntime>();
        materialSupply = new WorkOrderMaterialSupplyRuntime(
            this.itemStackRuntime, workforceReplanService, this.materialSources,
            constructionInputs, this.stateStore);
    }

    internal void BindPlacementService(
        GridBuildingPlacementService service)
    {
        if (service != null)
        {
            placementService = service;
        }
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        ContinuePendingRepairOutcomes();
        float cadenceTime = uiClock.Time;
        if (gameClock.IsPaused || cadenceTime < nextReadyConstructionReplanAt)
        {
            return;
        }

        nextReadyConstructionReplanAt = cadenceTime + 1f;
        RefreshWorkerAvailability();
        ContinuePendingFacilityRecoveries();
        ContinuePendingMaterialRestitutions();
        ContinuePendingTerminalOutcomes();
        string orphanedConstructionOrderId = ordersById.Values
            .Where(order => order.workTypeId == BuiltInWorkTypeIds.Construct
                && order.terminalPhase == WorkOrderTerminalPhase.None
                && order.status != WorkOrderStatus.Completed
                && order.status != WorkOrderStatus.Cancelled
                && !HasLiveConstructionSite(order.workOrderId))
            .Select(order => order.workOrderId)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(orphanedConstructionOrderId))
        {
            CancelOrder(
                orphanedConstructionOrderId,
                refundDeliveredMaterials: true);
            return;
        }

        if (ordersById.Values.Any(order =>
                order.workTypeId == BuiltInWorkTypeIds.Dismantle
                && order.status == WorkOrderStatus.Ready))
        {
            workforceReplanService.RequestOneWorkerToReplanFor(
                BuiltInWorkTypeIds.Dismantle,
                forceInterrupt: false);
            return;
        }

        if (!ordersById.Values.Any(order =>
                order.workTypeId == BuiltInWorkTypeIds.Construct
                && (order.status == WorkOrderStatus.Ready
                    || order.status == WorkOrderStatus.InProgress)
                && HasAvailableAutomaticConstructionSlot(order.workOrderId)))
        {
            return;
        }

        workforceReplanService.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.Construct,
            forceInterrupt: false);
    }

    public DungeonWorkOrderSaveData Capture()
    {
        if (destructiveDrainJoinConflict.Length > 0)
        {
            throw new InvalidOperationException(
                "A work-order destructive-drain join is unresolved: "
                + destructiveDrainJoinConflict);
        }
        WorkOrderAggregateState state = CurrentState;
        return new DungeonWorkOrderSaveData
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = Mathf.Max(1, state.NextOrderSequence),
            nextRepairOutcomeSequence = Mathf.Max(
                1,
                state.NextRepairOutcomeSequence),
            orders = state.OrdersById.Values
                .Where(order => order.status != WorkOrderStatus.Completed && order.status != WorkOrderStatus.Cancelled)
                .OrderBy(order => order.workOrderId, StringComparer.Ordinal)
                .Select(ToSaveData)
                .ToList(),
            qualityPipelines = state.QualityPipelinesById.Values
                .OrderBy(value => value.pipelineId, StringComparer.Ordinal)
                .Select(value => value.CloneNormalized())
                .ToList(),
            repairOutcomes = state.RepairOutcomesById.Values
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList(),
            pendingAccidents = state.PendingAccidents.Values
                .OrderBy(value => value.fact.operationId, StringComparer.Ordinal)
                .Select(value => value.Clone()).ToList()
        };
    }

    public void ValidateRestorePayload(DungeonWorkOrderSaveData snapshot)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        WorkOrderSaveValidation.Validate(
            snapshot,
            report,
            TryGetBuilding,
            ItemDefinitionExists);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Work-order restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    public WorkOrderRestoreCandidate PrepareRestoreCandidate(
        DungeonWorkOrderSaveData snapshot)
    {
        ValidateRestorePayload(snapshot);
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        WorkOrderAggregateState restored = BuildRestoredState(snapshot);
        if (!stateStore.TryGetRestoreGrid(out Grid restoreGrid))
        {
            throw new InvalidOperationException(
                "Work-order restore requires the detached facility grid candidate.");
        }

        List<WorkOrderConstructionSiteRestoreCandidate> candidates =
            new List<WorkOrderConstructionSiteRestoreCandidate>();
        foreach (WorkOrderRecord order in restored.OrdersById.Values
                     .Where(order => order.workTypeId == BuiltInWorkTypeIds.Construct)
                     .OrderBy(order => order.workOrderId, StringComparer.Ordinal))
        {
            if (!TryPrepareConstructionSiteCandidate(
                    restoreGrid,
                    order,
                    report,
                    out WorkOrderConstructionSiteRestoreCandidate candidate))
            {
                DiscardSiteCandidates(candidates);
                throw new InvalidOperationException(
                    "Work-order restore candidate could not prepare its detached construction sites: "
                    + string.Join(" | ", report.Errors));
            }

            candidates.Add(candidate);
        }

        return new WorkOrderRestoreCandidate(restored, candidates);
    }

    public void PublishRestoreCandidate(WorkOrderRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!restoreTransactionActive)
        {
            throw new InvalidOperationException(
                "Work-order publish requires the V18 save registry transaction boundary.");
        }
        if (restoreCandidatePrepared || stagedSiteCandidates.Count > 0)
        {
            throw new InvalidOperationException(
                "A work-order restore candidate was staged more than once.");
        }

        stateStore.Replace(candidate.State);
        stagedSiteCandidates.AddRange(candidate.TakeSites());
        restoreCandidatePrepared = true;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive || publishedSites != null)
        {
            throw new InvalidOperationException(
                "A work-order restore candidate is already active.");
        }

        ClearPendingDismantleObservations();
        destructiveDrainJoinConflict = string.Empty;
        restoreTransactionActive = true;
        restoreCandidatePrepared = false;
        stagedSiteCandidates = new List<WorkOrderConstructionSiteRestoreCandidate>();
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive || !restoreCandidatePrepared)
        {
            throw new InvalidOperationException(
                "No work-order restore candidate is ready to publish.");
        }

        Dictionary<ConstructionSite, string> publishedSites =
            new Dictionary<ConstructionSite, string>();
        foreach (WorkOrderConstructionSiteRestoreCandidate candidate in
                 stagedSiteCandidates)
        {
            if (candidate?.Site == null
                || !candidate.Site.IsDetachedRestoreCandidate
                || candidate.Site.gameObject.activeSelf)
            {
                throw new InvalidOperationException(
                    "A construction-site restore candidate is not detached and inactive.");
            }

            publishedSites.Add(candidate.Site, candidate.OrderId);
        }

        PublishedWorkOrderSites publication = new PublishedWorkOrderSites(
            new Dictionary<ConstructionSite, string>(orderIdBySite),
            stagedSiteCandidates);
        this.publishedSites = publication;
        orderIdBySite.Clear();
        foreach (KeyValuePair<ConstructionSite, string> pair in
                  publishedSites)
        {
            orderIdBySite.Add(pair.Key, pair.Value);
        }

        materialSupply.ReplaceForRestore();

        stagedSiteCandidates = null;
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        PublishedWorkOrderSites publication = publishedSites;
        if (publication == null)
        {
            DiscardRestoreCandidate();
            return;
        }

        orderIdBySite.Clear();
        foreach (KeyValuePair<ConstructionSite, string> pair in
                 publication.PreviousSites)
        {
            orderIdBySite.Add(pair.Key, pair.Value);
        }

        DiscardSiteCandidates(publication.Candidates);
        publishedSites = null;
        stagedSiteCandidates = null;
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
    }

    public void CompleteRestoreCandidate()
    {
        PublishedWorkOrderSites publication = publishedSites;
        if (publication == null)
        {
            return;
        }

        foreach (WorkOrderConstructionSiteRestoreCandidate candidate in
                 publication.Candidates)
        {
            if (candidate?.Site == null
                || !candidate.Site.IsDetachedRestoreCandidate
                || candidate.Site.gameObject.activeSelf)
            {
                throw new InvalidOperationException(
                    "A published construction-site candidate is no longer detached and inactive.");
            }
        }

        ClearRuntimeSites(publication.PreviousSites.Keys);
        foreach (WorkOrderConstructionSiteRestoreCandidate candidate in
                 publication.Candidates)
        {
            candidate.Site.PublishDetachedRestore();
            candidate.Site.gameObject.SetActive(true);
        }

        publishedSites = null;
        stagedSiteCandidates = null;
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
    }

    public void DiscardRestoreCandidate()
    {
        DiscardSiteCandidates(stagedSiteCandidates);
        stagedSiteCandidates = null;
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
    }

    public bool TryCreateConstructionOrder(
        ConstructionSite site,
        BuildingSO building,
        Vector2Int position,
        out string orderId,
        out string failureReason)
    {
        return TryCreateConstructionOrderCore(
            site,
            building,
            position,
            allowInstallationKit: true,
            out orderId,
            out failureReason);
    }

    internal bool TryCreateQualityRetryConstructionOrder(
        ConstructionSite site,
        BuildingSO building,
        Vector2Int position,
        out string orderId,
        out string failureReason)
    {
        return TryCreateConstructionOrderCore(
            site,
            building,
            position,
            allowInstallationKit: false,
            out orderId,
            out failureReason);
    }

    private bool TryCreateConstructionOrderCore(
        ConstructionSite site,
        BuildingSO building,
        Vector2Int position,
        bool allowInstallationKit,
        out string orderId,
        out string failureReason)
    {
        orderId = string.Empty;
        failureReason = string.Empty;
        if (site == null || building == null)
        {
            failureReason = "construction target missing";
            return false;
        }

        string createdOrderId = NextOrderId();
        WorkOrderRecord order = new WorkOrderRecord
        {
            workOrderId = createdOrderId,
            workTypeId = BuiltInWorkTypeIds.Construct,
            targetBuildingId = building.id,
            position = position,
            requiredWork = Mathf.Max(
                0.1f,
                balanceWorkCalculator?.CalculateConstruction(building)
                    ?? building.GetRequiredWork(BuiltInWorkTypeIds.Construct)),
            completedWork = 0f,
            preciseCompletedWork = 0d,
            materialDestinationId = BuildConstructionDestinationId(createdOrderId),
            status = WorkOrderStatus.WaitingForMaterials,
            workerPolicy = building.Abilities?
                .OfType<BuildingWorkAmountAbility>()
                .FirstOrDefault()?.DefaultWorkerPolicy
                ?? WorkerSelectionPolicySaveData.Anyone(
                    WorkerCandidateSortMode.SpecificThenBestExpectedQuality)
        };
        order.qualityPipelineId = order.workOrderId;
        order.qualityRoll = qualityResolver.Roll(
            unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
            order.qualityPipelineId,
            string.IsNullOrWhiteSpace(building.ContentDefinitionId)
                ? building.id.ToString()
                : building.ContentDefinitionId,
            0);

        if (!materialSupply.TryPrepareConstructionMaterials(
                order, building, site, allowInstallationKit, out failureReason))
        {
            return false;
        }

        ordersById[order.workOrderId] = order;
        orderIdBySite[site] = order.workOrderId;
        site.ConfigureWorkOrderRuntime(this);
        orderId = order.workOrderId;
        BumpWorkOrderCandidates();
        if (debugRules.IsEnabled(DungeonDebugCheat.InstantConstruction))
        {
            if (materialSupply.EnsureMaterialsReady(order, out failureReason))
            {
                CompleteOrder(order, site, out _, out failureReason);
            }
        }
        return true;
    }

    public bool TryGetOrderFor(
        BuildableObject target,
        WorkTypeId workTypeId,
        out WorkOrderProgressState order)
    {
        order = null;
        WorkOrderRecord record = FindOrder(target, workTypeId);
        if (record == null)
        {
            return false;
        }

        order = ToProgressState(record);
        return true;
    }

    public bool TryAttachConstructionPlacement(
        string orderId,
        string placementOperationId,
        IReadOnlyList<string> supportBuildingIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedOrderId = orderId?.Trim() ?? string.Empty;
        string normalizedOperationId = placementOperationId?.Trim()
            ?? string.Empty;
        string[] normalizedSupports = (supportBuildingIds
                ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!ordersById.TryGetValue(
                normalizedOrderId,
                out WorkOrderRecord order)
            || order.workTypeId != BuiltInWorkTypeIds.Construct
            || order.terminalPhase != WorkOrderTerminalPhase.None
            || !string.Equals(normalizedOperationId,
                WorkOrderPlacementIdentity.ForOrder(normalizedOrderId), StringComparison.Ordinal)
            || normalizedSupports.Any(value =>
                !DomainFactValueSyntax.IsIdentity(value))
            || normalizedSupports.Distinct(StringComparer.Ordinal).Count()
                != normalizedSupports.Length)
        {
            failureReason = "construction-placement-attachment-invalid";
            return false;
        }
        if (order.placementOperationId.Length > 0)
        {
            bool replay = string.Equals(
                    order.placementOperationId,
                    normalizedOperationId,
                    StringComparison.Ordinal)
                && order.supportBuildingIds.SequenceEqual(
                    normalizedSupports,
                    StringComparer.Ordinal);
            failureReason = replay
                ? string.Empty
                : "construction-placement-attachment-conflict";
            return replay;
        }

        order.placementOperationId = normalizedOperationId;
        order.supportBuildingIds.AddRange(normalizedSupports);
        BumpWorkOrderCandidates();
        return true;
    }

    public bool IsWorkerEligible(
        string orderId,
        CharacterActor actor,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(orderId)
            || !ordersById.TryGetValue(orderId.Trim(), out WorkOrderRecord order))
        {
            failureReason = "work order missing";
            return false;
        }

        return WorkerSelectionPolicyRules.IsEligible(
            order.workerPolicy,
            actor,
            narrativeQualifications,
            out failureReason);
    }

    public bool SetWorkerPolicy(
        string orderId,
        WorkerSelectionPolicySaveData policy,
        out DomainFailure failure)
    {
        failure = default;
        if (string.IsNullOrWhiteSpace(orderId)
            || !ordersById.TryGetValue(orderId.Trim(), out WorkOrderRecord order))
        {
            failure = new DomainFailure(FailureCode.WorkOrderMissing);
            return false;
        }
        if (policy == null
            || !Enum.IsDefined(typeof(WorkerSelectionMode), policy.mode)
            || !Enum.IsDefined(typeof(WorkerRequirementMatchMode), policy.matchMode)
            || !Enum.IsDefined(typeof(WorkerCandidateSortMode), policy.sortMode))
        {
            failure = new DomainFailure(FailureCode.WorkOrderInvalid);
            return false;
        }

        order.workerPolicy = policy.CloneNormalized();
        if (order.status == WorkOrderStatus.WaitingForEligibleWorker)
        {
            order.status = WorkOrderMaterialOutbox.HasAcknowledgedCustody(order)
                ? WorkOrderStatus.Ready
                : WorkOrderStatus.WaitingForMaterials;
        }
        BumpWorkOrderCandidates();
        return true;
    }

    public bool TryGetQualityPipeline(
        string pipelineId,
        out QualityTargetPipelineSaveData pipeline)
    {
        pipeline = null;
        if (string.IsNullOrWhiteSpace(pipelineId)
            || !CurrentState.QualityPipelinesById.TryGetValue(
                pipelineId.Trim(),
                out QualityTargetPipelineSaveData found))
        {
            return false;
        }
        pipeline = found.CloneNormalized();
        return true;
    }

    public CraftQualityAttemptEstimate CaptureQualityEstimate(string pipelineId)
    {
        if (string.IsNullOrEmpty(pipelineId)
            || !CurrentState.QualityPipelinesById.TryGetValue(pipelineId, out QualityTargetPipelineSaveData pipeline))
            return CraftQualityAttemptEstimate.Unavailable("품질 반복 주문 없음");
        WorkOrderRecord order = CurrentState.OrdersById.Values.FirstOrDefault(value =>
            value.qualityPipelineId == pipelineId && value.workTypeId == BuiltInWorkTypeIds.Construct);
        if (order == null) return CraftQualityAttemptEstimate.Unavailable("다음 시공 주문 준비 중 — 추정 보류");
        if (qualityResolver is not ICraftQualityProbabilityQuery probabilityQuery)
            return CraftQualityAttemptEstimate.Unavailable("현재 품질 계산기가 확률 미리보기를 제공하지 않음");
        float bestSkill = -1f;
        foreach (CharacterActor actor in characterWorld?.Characters ?? Array.Empty<CharacterActor>())
        {
            if (actor != null && WorkerSelectionPolicyRules.IsEligible(
                    order.workerPolicy, actor, narrativeQualifications, out _))
                bestSkill = Mathf.Max(bestSkill, GetConstructionQualitySkill(actor));
        }
        if (bestSkill < 0f)
            return CraftQualityAttemptEstimate.Unavailable("조건에 맞는 작업자 미정 — 확률 산정 보류");
        double probability = probabilityQuery.EstimateSuccessProbability(pipeline.minimumQuality,
            bestSkill, 0f, 0f, ResolveConstructionComplexityPenalty(order));
        return CraftQualityAttemptEstimate.Create(probability,
            pipeline.limitMode == QualityRepeatLimitMode.SafeLimits ? Mathf.Max(1, pipeline.maximumAttempts) : null,
            order.requiredWork, string.Empty, order.requiredItemMaterials.Values.Sum(),
            "현재 적격 작업자 중 최고 품질·단독 새 시공·조건 고정 추정 / 재료는 BOM 개수 합계"
            + (pipeline.workBudget > 0f ? " / WU 예산으로 조기 종료 가능" : string.Empty));
    }

    public bool CreateForWorkOrder(
        string workOrderId,
        QualityTargetPipelineSaveData request,
        out string pipelineId,
        out DomainFailure failure)
    {
        pipelineId = string.Empty;
        failure = DomainFailure.None;
        string normalizedOrderId = workOrderId?.Trim() ?? string.Empty;
        if (!ordersById.TryGetValue(normalizedOrderId, out WorkOrderRecord order)
            || request == null
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), request.minimumQuality)
            || !Enum.IsDefined(typeof(RejectedOutputDisposition), request.rejectedDisposition)
            || !Enum.IsDefined(typeof(QualityRepeatLimitMode), request.limitMode))
        {
            failure = new DomainFailure(FailureCode.WorkOrderInvalid);
            return false;
        }

        pipelineId = $"quality:{normalizedOrderId}";
        if (WritableState.QualityPipelinesById.ContainsKey(pipelineId))
        {
            failure = new DomainFailure(FailureCode.QualityPipelineBlocked);
            pipelineId = string.Empty;
            return false;
        }

        QualityTargetPipelineSaveData pipeline = request.CloneNormalized();
        pipeline.pipelineId = pipelineId;
        pipeline.definitionId = string.IsNullOrWhiteSpace(pipeline.definitionId)
            ? order.targetBuildingId.ToString()
            : pipeline.definitionId;
        pipeline.facilityPipeline = order.workTypeId == BuiltInWorkTypeIds.Construct;
        pipeline.workerPolicy = pipeline.workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
        pipeline.attemptIndex = 0;
        pipeline.acceptedCount = 0;
        pipeline.consumedWork = 0f;
        pipeline.stage = QualityTargetPipelineStage.WaitingForMaterials;
        pipeline.footprintX = order.position.x;
        pipeline.footprintY = order.position.y;
        pipeline.currentRoll = qualityResolver.Roll(
            unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
            pipelineId,
            pipeline.definitionId,
            0);
        WritableState.QualityPipelinesById.Add(pipelineId, pipeline);
        order.qualityPipelineId = pipelineId;
        order.qualityAttemptIndex = 0;
        order.qualityRoll = CloneRoll(pipeline.currentRoll);
        order.workerPolicy = pipeline.workerPolicy.CloneNormalized();
        if (!CanPotentiallyReachQuality(order, pipeline.minimumQuality))
        {
            order.status = WorkOrderStatus.TargetCurrentlyUnreachable;
            pipeline.stage = QualityTargetPipelineStage.TargetCurrentlyUnreachable;
            itemStackRuntime.ReleaseStacksByDestination(
                order.materialDestinationId,
                order.position);
        }
        BumpWorkOrderCandidates();
        return true;
    }

    public bool PauseQualityPipeline(string pipelineId, out DomainFailure failure) =>
        ChangePipelineStage(
            pipelineId,
            QualityTargetPipelineStage.Paused,
            allowTerminal: false,
            out failure);

    public bool ResumeQualityPipeline(string pipelineId, out DomainFailure failure)
    {
        if (!TryGetMutablePipeline(pipelineId, out QualityTargetPipelineSaveData pipeline,
                out failure)
            || pipeline.IsTerminal)
        {
            return false;
        }
        pipeline.stage = pipeline.HasReachedSafeLimit
            ? QualityTargetPipelineStage.Paused
            : QualityTargetPipelineStage.WaitingForMaterials;
        if (!pipeline.HasReachedSafeLimit)
        {
            foreach (WorkOrderRecord order in ordersById.Values.Where(value =>
                         value != null
                         && value.workTypeId == BuiltInWorkTypeIds.Construct
                         && string.Equals(
                             value.qualityPipelineId,
                             pipeline.pipelineId,
                             StringComparison.Ordinal)))
            {
                order.status = WorkOrderStatus.WaitingForMaterials;
                materialSupply.RequestMissingMaterials(order);
            }
        }
        BumpWorkOrderCandidates();
        return !pipeline.HasReachedSafeLimit;
    }

    public bool CancelQualityPipeline(
        string pipelineId,
        out DomainFailure failure)
    {
        if (!TryGetMutablePipeline(
                pipelineId,
                out QualityTargetPipelineSaveData pipeline,
                out failure)
            || pipeline.IsTerminal)
        {
            return false;
        }

        foreach (WorkOrderRecord order in ordersById.Values
                     .Where(value => value != null
                         && string.Equals(
                             value.qualityPipelineId,
                             pipeline.pipelineId,
                             StringComparison.Ordinal))
                     .ToArray())
        {
            if (order.workTypeId == BuiltInWorkTypeIds.Construct)
            {
                // Preserve the already placed site. It may finish once, but no
                // longer feeds another quality attempt.
                order.qualityPipelineId = string.Empty;
                continue;
            }

            if (order.workTypeId == BuiltInWorkTypeIds.Dismantle)
            {
                if (!order.facilityRemovedForRetry
                    && gridSystemProvider.TryGetGrid(out Grid grid))
                {
                    BuildableObject target = grid.GetGridCell(order.position)?
                        .GetAllOccupants()
                        .OfType<BuildableObject>()
                        .FirstOrDefault(value => value != null
                            && value.id == order.targetBuildingId);
                    target?.GetComponent<RuntimeWorkCapabilityMarker>()?
                        .RemoveSource(pipeline.pipelineId);
                }
                if (!string.IsNullOrEmpty(order.destructiveDrainOperationId)
                    || order.facilityRemovedForRetry)
                {
                    // An accepted destructive drain is irreversible.  Cancelling
                    // the quality loop may suppress the rebuild, but the order
                    // must remain as the durable owner of physical salvage until
                    // world removal and exact recovery publication finish.
                    order.cancelRebuildAfterDestructiveDrain = true;
                    order.status = order.facilityRemovedForRetry
                        ? WorkOrderStatus.WaitingForOutputSpace
                        : WorkOrderStatus.Blocked;
                    continue;
                }

                RemoveDismantleObservation(order.workOrderId);
                ordersById.Remove(order.workOrderId);
            }
        }

        pipeline.stage = QualityTargetPipelineStage.Cancelled;
        facilityCandidateCache?.Clear();
        BumpWorkOrderCandidates();
        return true;
    }

    public bool ApplyWork(
        CharacterActor worker,
        BuildableObject target,
        WorkTypeId workTypeId,
        float amount,
        out bool completed,
        out bool appliedCompletionEffects,
        out string message)
    {
        completed = false;
        appliedCompletionEffects = false;
        message = string.Empty;
        WorkOrderRecord order = FindOrder(target, workTypeId);
        if (order == null)
        {
            message = "work order missing";
            return false;
        }
        if (worker != null
            && !WorkerSelectionPolicyRules.IsEligible(
                order.workerPolicy,
                worker,
                narrativeQualifications,
                out message))
        {
            return false;
        }

        WorkTypeDefinition definition = WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition resolvedDefinition)
            ? resolvedDefinition
            : null;
        string displayName = definition?.DisplayName ?? workTypeId.ToString();
        if (order.status == WorkOrderStatus.WaitingForMaterials && !materialSupply.EnsureMaterialsReady(order, out message))
        {
            materialSupply.RequestMissingMaterials(order);
            return false;
        }

        if (order.status == WorkOrderStatus.Blocked
            || order.status == WorkOrderStatus.Cancelled
            || order.status == WorkOrderStatus.Completed
            || order.status == WorkOrderStatus.TargetCurrentlyUnreachable
            || order.status == WorkOrderStatus.WaitingForOutputSpace)
        {
            message = order.status.ToString();
            return false;
        }

        order.status = WorkOrderStatus.InProgress;
        order.reservedWorkerPersistentId = worker?.Identity?.PersistentId ?? string.Empty;
        if (debugRules.IsEnabled(DungeonDebugCheat.InstantWork)
            || (workTypeId == BuiltInWorkTypeIds.Construct
                && debugRules.IsEnabled(DungeonDebugCheat.InstantConstruction)))
        {
            amount = order.requiredWork;
        }
        float acceptedWork = Mathf.Min(
            Mathf.Max(0f, amount),
            Mathf.Max(0f, order.requiredWork - order.completedWork));
        order.preciseCompletedWork = Math.Min(
            Math.Max(0.1d, order.requiredWork),
            Math.Max(0d, order.preciseCompletedWork + acceptedWork));
        order.completedWork = (float)order.preciseCompletedWork;
        if (worker != null && acceptedWork > 0f)
        {
            CraftContributionAccumulator contribution =
                new CraftContributionAccumulator(order.contributions);
            contribution.Add(
                worker.Identity?.PersistentId,
                acceptedWork,
                GetConstructionQualitySkill(worker));
            order.contributions.Clear();
            order.contributions.AddRange(contribution.Capture());
        }

        if (order.completedWork + 0.001f < order.requiredWork)
        {
            message = $"{displayName} {Mathf.RoundToInt(order.completedWork / Mathf.Max(0.1f, order.requiredWork) * 100f)}%";
            return true;
        }

        completed = CompleteOrder(order, target, out appliedCompletionEffects, out message);
        return completed;
    }

    public bool TryJoinConstructionProject(
        BuildableObject target,
        CharacterActor worker,
        out ProjectWorkerLease lease,
        out string failureReason)
    {
        lease = null;
        failureReason = string.Empty;
        WorkOrderRecord order = FindOrder(target, BuiltInWorkTypeIds.Construct);
        if (order == null || target?.BuildingData == null || worker == null)
        {
            failureReason = "건설 프로젝트 또는 작업자 권위를 확인할 수 없습니다.";
            return false;
        }
        if (projectWorkforce == null)
        {
            failureReason = "건설 프로젝트 인력 서비스가 등록되지 않았습니다.";
            return false;
        }
        if (!CharacterPersistentIdentity.TryGet(worker, out CharacterId workerId))
        {
            failureReason = "건설 작업자의 영속 ID를 확인할 수 없습니다.";
            return false;
        }

        ProjectScale scale = target.BuildingData.GetConstructionProjectScale();
        return projectWorkforce.TryJoin(
            BuildConstructionProjectId(order.workOrderId),
            workerId.Value,
            scale,
            SettlementLaborBalanceRules.GetMaximumWorkers(scale),
            out lease,
            out failureReason);
    }

    public bool UpdateConstructionWorkerRate(
        BuildableObject target,
        CharacterActor worker,
        float wuPerSecond)
    {
        WorkOrderRecord order = FindOrder(target, BuiltInWorkTypeIds.Construct);
        return order != null
            && projectWorkforce != null
            && CharacterPersistentIdentity.TryGet(worker, out CharacterId workerId)
            && projectWorkforce.UpdateWorkerRate(
                BuildConstructionProjectId(order.workOrderId),
                workerId.Value,
                wuPerSecond);
    }

    public float GetConstructionContributionMultiplier(
        BuildableObject target,
        CharacterActor worker)
    {
        WorkOrderRecord order = FindOrder(target, BuiltInWorkTypeIds.Construct);
        return order != null
            && projectWorkforce != null
            && CharacterPersistentIdentity.TryGet(worker, out CharacterId workerId)
                ? projectWorkforce.GetContributionMultiplier(
                    BuildConstructionProjectId(order.workOrderId),
                    workerId.Value)
                : 0f;
    }

    public bool TryCaptureConstructionProject(
        BuildableObject target,
        out ProjectWorkforceSnapshot snapshot)
    {
        WorkOrderRecord order = FindOrder(target, BuiltInWorkTypeIds.Construct);
        if (order == null || target?.BuildingData == null || projectWorkforce == null)
        {
            snapshot = default;
            return false;
        }

        string projectId = BuildConstructionProjectId(order.workOrderId);
        if (projectWorkforce.TryCapture(projectId, out snapshot))
            return true;

        ProjectScale scale = target.BuildingData.GetConstructionProjectScale();
        snapshot = new ProjectWorkforceSnapshot(
            projectId,
            scale,
            0,
            SettlementLaborBalanceRules.GetMaximumWorkers(scale),
            SettlementLaborBalanceRules.GetDefaultAutomaticWorkerLimit(scale),
            0f,
            SettlementLaborBalanceRules.GetWorkerContribution(scale, 0),
            0f,
            0f);
        return true;
    }

    private static string BuildConstructionProjectId(string orderId) =>
        $"construction:{orderId?.Trim() ?? string.Empty}";

    public bool RefreshMaterialsReady(ConstructionSite site)
    {
        WorkOrderRecord order = FindOrder(site, BuiltInWorkTypeIds.Construct);
        if (order == null)
        {
            return false;
        }

        if (order.status == WorkOrderStatus.Ready || order.status == WorkOrderStatus.InProgress)
        {
            return true;
        }

        bool ready = materialSupply.EnsureMaterialsReady(order, out _);
        if (!ready)
        {
            materialSupply.RequestMissingMaterials(order);
        }
        else
        {
            BumpWorkOrderCandidates();
            workforceReplanService?.RequestOneWorkerToReplanFor(
                BuiltInWorkTypeIds.Construct,
                forceInterrupt: false);
        }

        return ready;
    }

    public bool CancelOrder(string orderId, bool refundDeliveredMaterials)
    {
        if (string.IsNullOrWhiteSpace(orderId)
            || !ordersById.TryGetValue(orderId, out WorkOrderRecord order))
        {
            return false;
        }

        if (order.terminalPhase ==
            WorkOrderTerminalPhase.EffectsAppliedPendingOutcome)
        {
            return order.terminalKind == WorkOrderTerminalKind.Cancelled
                && TryFinalizeConstructionTerminal(
                    order,
                    FindConstructionSite(order.workOrderId),
                    out _);
        }

        if (!materialSupply.TryReleaseForCancellation(order, refundDeliveredMaterials))
        {
            return false;
        }
        if (order.workTypeId == BuiltInWorkTypeIds.Construct
            && !string.IsNullOrEmpty(order.placementOperationId))
        {
            order.terminalKind = WorkOrderTerminalKind.Cancelled;
            order.terminalPhase =
                WorkOrderTerminalPhase.EffectsAppliedPendingOutcome;
            order.status = WorkOrderStatus.Blocked;
            return TryFinalizeConstructionTerminal(
                order,
                FindConstructionSite(order.workOrderId),
                out _);
        }
        order.status = WorkOrderStatus.Cancelled;
        ordersById.Remove(orderId);
        foreach (KeyValuePair<ConstructionSite, string> pair in orderIdBySite.ToArray())
        {
            if (string.Equals(pair.Value, orderId, StringComparison.Ordinal))
            {
                orderIdBySite.Remove(pair.Key);
            }
        }

        BumpWorkOrderCandidates();
        return true;
    }

    private ConstructionSite FindConstructionSite(string orderId) =>
        orderIdBySite.FirstOrDefault(pair => string.Equals(
            pair.Value,
            orderId,
            StringComparison.Ordinal)).Key;

    private bool HasAvailableAutomaticConstructionSlot(string orderId)
    {
        foreach (KeyValuePair<ConstructionSite, string> pair in orderIdBySite)
        {
            if (string.Equals(pair.Value, orderId, StringComparison.Ordinal)
                && pair.Key != null
                && !pair.Key.IsGridDestroyed
                && pair.Key.TargetBuilding != null)
            {
                ProjectScale scale = pair.Key.TargetBuilding.GetConstructionProjectScale();
                int automaticLimit = SettlementLaborBalanceRules
                    .GetDefaultAutomaticWorkerLimit(scale);
                return pair.Key.OccupiedWorkerSlotCount < automaticLimit;
            }
        }

        return false;
    }

    private bool HasLiveConstructionSite(string orderId)
    {
        return orderIdBySite.Any(pair =>
            string.Equals(pair.Value, orderId, StringComparison.Ordinal)
            && pair.Key != null
            && !pair.Key.IsGridDestroyed);
    }

    public bool DebugCompleteOrder(string orderId, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(orderId)
            || !ordersById.TryGetValue(orderId, out WorkOrderRecord order))
        {
            message = "작업 주문을 찾을 수 없습니다.";
            return false;
        }
        if (order.terminalPhase ==
            WorkOrderTerminalPhase.EffectsAppliedPendingOutcome)
        {
            return TryFinalizeConstructionTerminal(
                order,
                FindConstructionSite(order.workOrderId),
                out message);
        }

        BuildableObject target = ResolveTarget(order);
        if (target == null)
        {
            message = "작업 대상을 찾을 수 없습니다.";
            return false;
        }
        if (!materialSupply.EnsureMaterialsReady(order, out message))
        {
            materialSupply.RequestMissingMaterials(order);
            return false;
        }

        order.completedWork = order.requiredWork;
        order.preciseCompletedWork = order.requiredWork;
        return CompleteOrder(order, target, out _, out message);
    }

    public int DebugCompleteAllOrders()
    {
        int completed = 0;
        foreach (string orderId in ordersById.Keys.ToArray())
        {
            if (DebugCompleteOrder(orderId, out _))
            {
                completed++;
            }
        }

        return completed;
    }

    private BuildableObject ResolveTarget(WorkOrderRecord order)
    {
        if (order == null)
        {
            return null;
        }

        if (order.workTypeId == BuiltInWorkTypeIds.Construct)
        {
            return orderIdBySite.FirstOrDefault(pair =>
                string.Equals(pair.Value, order.workOrderId, StringComparison.Ordinal)).Key;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return null;
        }

        return grid.GetGridCell(order.position)?
            .GetAllOccupants()
            .OfType<BuildableObject>()
            .FirstOrDefault(building => building != null && building.id == order.targetBuildingId);
    }

    private bool CompleteOrder(
        WorkOrderRecord order,
        BuildableObject target,
        out bool appliedCompletionEffects,
        out string message)
    {
        appliedCompletionEffects = false;
        message = string.Empty;
        CraftContributionAccumulator contribution =
            new CraftContributionAccumulator(order.contributions);
        CraftQualityResolution quality = qualityResolver.Resolve(
            order.qualityRoll,
            contribution.WeightedRelevantSkill > 0f
                ? contribution.WeightedRelevantSkill
                : 50f,
            facilityBonus: 0f,
            toolBonus: 0f,
            complexityPenalty: ResolveConstructionComplexityPenalty(order));
        if (order.workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            return CompleteFacilityDismantle(
                order,
                target,
                contribution.WeightedRelevantSkill,
                out appliedCompletionEffects,
                out message);
        }
        if (target is ConstructionSite site)
        {
            if (order.terminalPhase ==
                WorkOrderTerminalPhase.EffectsAppliedPendingOutcome)
            {
                appliedCompletionEffects = true;
                return TryFinalizeConstructionTerminal(
                    order,
                    site,
                    out message);
            }
            if (!materialSupply.TryPrepareCompletion(
                    order, out WorkConstructionInputOwnerDescriptor descriptor, out message))
            {
                return false;
            }
            bool placed = site.CompleteConstruction(
                out BuildingPlacementCommitReceipt placementReceipt);
            if (!placed)
            {
                order.status = WorkOrderStatus.Blocked;
                message = "construction completion failed";
                return false;
            }
            if (descriptor != null
                && !materialSupply.TryRetireAfterCompletion(descriptor, out message))
            {
                throw new InvalidOperationException(
                    "Construction committed but its input owner could not retire: "
                    + message);
            }
            if (!placementReceipt.IsValid)
            {
                // Legacy/editor-only construction callbacks predate the
                // production placement receipt contract. Production grid
                // placement always attaches a canonical operation first.
                site.RemoveSiteOnly();
                order.status = WorkOrderStatus.Completed;
                order.completedWork = order.requiredWork;
                order.preciseCompletedWork = order.requiredWork;
                ordersById.Remove(order.workOrderId);
                orderIdBySite.Remove(site);
                BumpWorkOrderCandidates();
                appliedCompletionEffects = true;
                BuildableObject legacyBuilding =
                    ApplyCompletedBuildingQuality(order, quality);
                ResolveQualityPipelineAttempt(order, quality, legacyBuilding);
                message = "construction completed (legacy fixture)";
                return true;
            }

            order.terminalKind = WorkOrderTerminalKind.Completed;
            order.terminalPhase =
                WorkOrderTerminalPhase.EffectsAppliedPendingOutcome;
            order.finalFacilityId = placementReceipt.FinalFacilityId;
            order.finalFacilityDefinitionId =
                placementReceipt.FinalFacilityDefinitionId;
            order.replacedFacilityId =
                placementReceipt.ReplacedFacilityId;
            order.replacedFacilityDefinitionId =
                placementReceipt.ReplacedFacilityDefinitionId;
            order.status = WorkOrderStatus.Blocked;
            order.completedWork = order.requiredWork;
            order.preciseCompletedWork = order.requiredWork;
            appliedCompletionEffects = true;
            BuildableObject completedBuilding =
                ApplyCompletedBuildingQuality(order, quality);
            ResolveQualityPipelineAttempt(
                order,
                quality,
                completedBuilding);
            return TryFinalizeConstructionTerminal(
                order,
                site,
                out message);
        }

        order.status = WorkOrderStatus.Completed;
        order.completedWork = order.requiredWork;
        order.preciseCompletedWork = order.requiredWork;
        ordersById.Remove(order.workOrderId);
        BumpWorkOrderCandidates();
        message = "work completed";
        return true;
    }

    private bool TryFinalizeConstructionTerminal(
        WorkOrderRecord order,
        ConstructionSite site,
        out string message)
    {
        if (!TryPublishConstructionTerminalFact(order, out message))
        {
            order.status = WorkOrderStatus.Blocked;
            BumpWorkOrderCandidates();
            return false;
        }

        order.terminalPhase = WorkOrderTerminalPhase.OutcomeCommitted;
        order.status = order.terminalKind == WorkOrderTerminalKind.Completed
            ? WorkOrderStatus.Completed
            : WorkOrderStatus.Cancelled;
        ordersById.Remove(order.workOrderId);
        if (site != null)
        {
            orderIdBySite.Remove(site);
            site.RemoveSiteOnly();
        }
        else
        {
            RemoveConstructionSiteMapping(order.workOrderId, removeSite: true);
        }
        BumpWorkOrderCandidates();
        message = order.terminalKind == WorkOrderTerminalKind.Completed
            ? "construction completed"
            : "construction cancelled";
        return true;
    }

    private bool TryPublishConstructionTerminalFact(
        WorkOrderRecord order,
        out string failureReason)
    {
        failureReason = string.Empty;
        LastTerminalOutcomeFailureForDebug = string.Empty;
        if (order == null || order.workTypeId != BuiltInWorkTypeIds.Construct
            || order.terminalPhase != WorkOrderTerminalPhase.EffectsAppliedPendingOutcome
            || order.terminalKind == WorkOrderTerminalKind.None
            || !DomainFactValueSyntax.IsIdentity(order.placementOperationId))
        {
            failureReason = "construction-terminal-state-invalid";
            LastTerminalOutcomeFailureForDebug = failureReason;
            return false;
        }

        try
        {
            // Effects are already owned by this order. Capture their business
            // result once; later retries must not recalculate day/name/quality.
            if (!order.terminalFactCaptured)
            {
                ConstructionWorkEndedData snapshot = CaptureConstructionTerminal(order);
                snapshot.Validate();
                order.terminalFact = snapshot;
                order.terminalFactCaptured = true;
                BumpWorkOrderCandidates();
            }
            var fact = new ConstructionWorkEndedFact(order.terminalFact);
            DomainFactDelivery delivery = PublishConstructionTerminal(order, fact);
            bool confirmed = delivery.Phase is DomainFactDeliveryPhase.Delivered
                or DomainFactDeliveryPhase.AcceptedPendingDelivery;
            if (confirmed && delivery.OperationKey == order.terminalPublicationKey
                && !string.IsNullOrEmpty(delivery.PublicationId)
                && delivery.Fingerprint.Length == 64)
                return true;
            failureReason = "construction-terminal-publication-unconfirmed:" + delivery.Detail;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            // Keep applied effects and the reserved identity on an unknown ACK.
            failureReason = "construction-terminal-publication-unconfirmed:" + exception.Message;
        }
        LastTerminalOutcomeFailureForDebug = failureReason;
        return false;
    }

    private ConstructionWorkEndedData CaptureConstructionTerminal(WorkOrderRecord order)
    {
        BuildingSO building = TryGetBuilding(order.targetBuildingId)
            ?? throw new InvalidOperationException("construction-terminal-definition-missing");
        var contribution = new CraftContributionAccumulator(order.contributions);
        CraftQualityResolution quality = qualityResolver.Resolve(order.qualityRoll,
            contribution.WeightedRelevantSkill > 0f ? contribution.WeightedRelevantSkill : 50f,
            facilityBonus: 0f, toolBonus: 0f,
            complexityPenalty: ResolveConstructionComplexityPenalty(order));
        WorkOrderSaveData source = ToSaveData(order);
        return new ConstructionWorkEndedData
        {
            workOrderId = source.workOrderId, terminalKind = source.terminalKind,
            placementOperationId = source.placementOperationId,
            constructionSitePersistentId = source.constructionSitePersistentId,
            targetBuildingId = source.targetBuildingId,
            buildingDefinitionId = ResolveBuildingDefinitionId(building),
            buildingDisplayName = building.objectName,
            finalFacilityId = source.finalFacilityId,
            finalFacilityDefinitionId = source.finalFacilityDefinitionId,
            replacedFacilityId = source.replacedFacilityId,
            replacedFacilityDefinitionId = source.replacedFacilityDefinitionId,
            absoluteDay = Math.Max(1, runFlow?.CurrentDay ?? 1),
            gridX = source.gridX, gridY = source.gridY,
            requiredWork = source.requiredWork, completedWork = source.completedWork,
            qualityTier = quality.Tier, supportBuildingIds = source.supportBuildingIds,
            contributions = source.contributions.OrderBy(value => value.characterId, StringComparer.Ordinal).ToList(),
            itemMaterials = source.itemMaterials,
            materialInputOperationId = source.materialTransfer.operationId,
            restitutionOperationId = source.materialTransfer.restitutionOperationId
        };
    }

    private DomainFactDelivery PublishConstructionTerminal(WorkOrderRecord order, ConstructionWorkEndedFact fact)
    {
        IDomainFactPublisher publisher = constructionFacts ??= ResolveOptional<IDomainFactPublisher>();
        if (publisher is not IDomainFactRecoveryPublisher recovery)
            return DomainFactDelivery.Reject("construction-terminal-fact-publication-unavailable");
        if (string.IsNullOrEmpty(order.terminalPublicationKey))
        {
            if (!publisher.TryReserve(fact, out DomainFactLease lease, out string failure))
                return DomainFactDelivery.Reject(failure);
            string key = lease.PublicationKey;
            if (!DomainFactValueSyntax.IsText(key, 1024, false))
            {
                publisher.Cancel(lease);
                return DomainFactDelivery.Reject("construction-terminal-publication-key-missing");
            }
            order.terminalPublicationKey = key;
            BumpWorkOrderCandidates();
            DomainFactDelivery delivery = publisher.Publish(lease, fact);
            if (delivery.Phase == DomainFactDeliveryPhase.Rejected)
            {
                order.terminalPublicationKey = string.Empty;
                BumpWorkOrderCandidates();
            }
            return delivery;
        }
        var results = new DomainFactDelivery[1];
        return recovery.ReplayBatch(new[] { DomainFactReplay.Create(order.terminalPublicationKey, fact) },
            results, out string replayFailure)
            ? results[0] : DomainFactDelivery.Reject(replayFailure);
    }

    private static string ResolveBuildingDefinitionId(BuildingSO building) =>
        !string.IsNullOrWhiteSpace(building?.ContentDefinitionId)
            ? building.ContentDefinitionId.Trim()
            : "building:" + Math.Max(1, building?.id ?? 1);

    private void RemoveConstructionSiteMapping(
        string orderId,
        bool removeSite)
    {
        foreach (KeyValuePair<ConstructionSite, string> pair in
                 orderIdBySite.ToArray())
        {
            if (!string.Equals(pair.Value, orderId, StringComparison.Ordinal))
            {
                continue;
            }
            orderIdBySite.Remove(pair.Key);
            if (removeSite)
            {
                pair.Key?.RemoveSiteOnly();
            }
        }
    }

    private WorkOrderRecord FindOrder(BuildableObject target, WorkTypeId workTypeId)
    {
        if (target is ConstructionSite site
            && orderIdBySite.TryGetValue(site, out string orderId)
            && ordersById.TryGetValue(orderId, out WorkOrderRecord bySite)
            && IsOrderWorkType(bySite, workTypeId))
        {
            return bySite;
        }

        if (target == null)
        {
            return null;
        }

        return ordersById.Values.FirstOrDefault(order =>
            IsOrderWorkType(order, workTypeId)
            && order.targetBuildingId == target.id
            && order.position == target.centerPos);
    }

    private static bool IsOrderWorkType(WorkOrderRecord order, WorkTypeId workTypeId)
    {
        return order != null
            && workTypeId.IsValid
            && order.workTypeId == workTypeId;
    }

    private void ContinuePendingMaterialRestitutions()
    {
        foreach (WorkOrderRecord order in ordersById.Values
                     .Where(value => value?.materialTransfer?.Phase ==
                         WorkOrderMaterialTransferPhase.RestitutionPending)
                     .OrderBy(value => value.workOrderId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!materialSupply.TryPublishRestitution(order, out _))
            {
                continue;
            }

            if (order.workTypeId == BuiltInWorkTypeIds.Construct
                && !string.IsNullOrEmpty(order.placementOperationId))
            {
                CancelOrder(order.workOrderId, refundDeliveredMaterials: true);
                continue;
            }

            order.status = WorkOrderStatus.Cancelled;
            ordersById.Remove(order.workOrderId);
            foreach (KeyValuePair<ConstructionSite, string> pair in
                     orderIdBySite.ToArray())
            {
                if (!string.Equals(
                        pair.Value,
                        order.workOrderId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                orderIdBySite.Remove(pair.Key);
                pair.Key?.RemoveSiteOnly();
            }
            BumpWorkOrderCandidates();
        }
    }

    private void ContinuePendingTerminalOutcomes()
    {
        foreach (WorkOrderRecord order in ordersById.Values
                     .Where(value => value?.terminalPhase ==
                         WorkOrderTerminalPhase.EffectsAppliedPendingOutcome)
                     .OrderBy(value => value.workOrderId, StringComparer.Ordinal)
                     .ToArray())
        {
            TryFinalizeConstructionTerminal(
                order,
                FindConstructionSite(order.workOrderId),
                out _);
        }
    }

    private bool TryPrepareConstructionSiteCandidate(
        Grid grid,
        WorkOrderRecord order,
        DungeonGameRestoreReport report,
        out WorkOrderConstructionSiteRestoreCandidate candidate)
    {
        candidate = null;
        if (grid == null)
        {
            report.AddError(
                $"Cannot prepare construction site {order.workOrderId}: restore grid missing.");
            return false;
        }

        BuildingSO building = TryGetBuilding(order.targetBuildingId);
        if (building == null)
        {
            report.AddError(
                $"Cannot prepare construction site {order.workOrderId}: building {order.targetBuildingId} missing.");
            return false;
        }

        IReadOnlyList<Vector2Int> positions =
            building.GetGridPosList(order.position);
        if (positions == null
            || positions.Count == 0
            || positions.Any(position => !grid.IsValidGridPos(position)))
        {
            report.AddError(
                $"Cannot prepare construction site {order.workOrderId}: footprint is outside the restore grid.");
            return false;
        }

        GameObject siteObject = new GameObject(
            $"ConstructionSite_{building.objectName}_{order.position.x}_{order.position.y}");
        siteObject.SetActive(false);
        DungeonRuntimeHierarchy.Parent(siteObject, DungeonRuntimeHierarchy.Construction);
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        try
        {
            site.PrepareForDetachedRestore();
            objectResolver.Inject(site);
            site.RestorePersistentIdentity(
                (BuildingInstanceId)order.constructionSitePersistentId);
            site.transform.position = grid.GetWorldPos(order.position);
            site.SetGrid(grid);
            site.Initialization(building, order.position);
            site.ConfigureWorkOrderRuntime(this);
            site.ConfigureSite(
                order.workOrderId,
                () => TryPlaceFinalBuildingOnRestore(
                    grid,
                    building,
                    order),
                () => RemoveSiteFromGrid(grid, site));

            if (!grid.RegisterOccupant(
                    site,
                    GridLayer.Construction,
                    positions,
                    false))
            {
                site.DiscardDetachedRestore();
                report.AddError(
                    $"Cannot prepare construction site {order.workOrderId}: grid occupied.");
                return false;
            }

            candidate = new WorkOrderConstructionSiteRestoreCandidate(
                order.workOrderId,
                site);
            return true;
        }
        catch (Exception exception)
        {
            if (site != null && site.IsDetachedRestoreCandidate)
            {
                site.DiscardDetachedRestore();
            }
            else if (siteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(siteObject);
            }

            report.AddError(
                $"Cannot prepare construction site {order.workOrderId}: {exception.Message}");
            return false;
        }
    }

    private BuildingPlacementCommitReceipt TryPlaceFinalBuildingOnRestore(
        Grid grid,
        BuildingSO building,
        WorkOrderRecord order)
    {
        GridBuildingPlacementService service = placementService;
        if (service == null)
        {
            IGridBuildingFactory restoredBuildingFactory =
                new GridBuildingFactory(
                    buildingVisual: null,
                    onBuildingCreated: objectResolver.Inject,
                    objectFactory: new GridBuildingObjectFactory());
            service = new GridBuildingPlacementService(
                grid,
                null,
                TryGetBuilding,
                restoredBuildingFactory,
                new BuildingPlacementValidator(),
                this);
        }
        return service.TryPlaceBuildingImmediateUncheckedWithReceipt(
            building,
            order.position,
            chargeCost: false,
            out _);
    }

    private static void ClearRuntimeSites(
        IEnumerable<ConstructionSite> sites)
    {
        foreach (ConstructionSite site in
                 (sites ?? Enumerable.Empty<ConstructionSite>()).ToArray())
        {
            if (site == null)
            {
                continue;
            }

            site.RetireForWorldReplacement();
        }
    }

    private sealed class PublishedWorkOrderSites
    {
        internal PublishedWorkOrderSites(
            IReadOnlyDictionary<ConstructionSite, string> previousSites,
            IReadOnlyList<WorkOrderConstructionSiteRestoreCandidate> candidates)
        {
            PreviousSites = previousSites
                ?? throw new ArgumentNullException(nameof(previousSites));
            Candidates = candidates
                ?? throw new ArgumentNullException(nameof(candidates));
        }

        internal IReadOnlyDictionary<ConstructionSite, string> PreviousSites
        {
            get;
        }

        internal IReadOnlyList<WorkOrderConstructionSiteRestoreCandidate>
            Candidates
        {
            get;
        }
    }

    private static void RemoveSiteFromGrid(Grid grid, ConstructionSite site)
    {
        if (site == null)
        {
            return;
        }

        grid?.RemoveOccupant(
                site,
                GridLayer.Construction,
                site.buildPoses,
                false);
        DestroyUnityObject(site.gameObject);
    }

    internal static void DiscardSiteCandidates(
        IEnumerable<WorkOrderConstructionSiteRestoreCandidate> candidates)
    {
        foreach (WorkOrderConstructionSiteRestoreCandidate candidate in
                 candidates ?? Enumerable.Empty<WorkOrderConstructionSiteRestoreCandidate>())
        {
            ConstructionSite site = candidate?.Site;
            if (site == null)
            {
                continue;
            }

            if (site.IsDetachedRestoreCandidate)
            {
                site.DiscardDetachedRestore();
            }
            else
            {
                site.RetireForWorldReplacement();
            }
        }
    }

    private static void DestroyUnityObject(UnityEngine.Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(value);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(value);
        }
    }

    private BuildingSO TryGetBuilding(int id)
    {
        try
        {
            return buildingDefinitionLookup.GetBuilding(id);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool ItemDefinitionExists(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId)
            && itemStackRuntime.CatalogProvider != null
            && itemStackRuntime.CatalogProvider.TryGetDefinition(
                itemId,
                out _);
    }

    private string NextOrderId()
    {
        WorkOrderAggregateState state = WritableState;
        return $"work:{state.NextOrderSequence++:D6}";
    }

    [GameplayInternalOnly(
        "Consumes the existing persisted work sequence before one work-accident damage commit.",
        "WorkTaskExecutor.TryTriggerWorkAccident only")]
    string IWorkAccidentOperationIdAuthority
        .AllocateWorkAccidentOperationId(
            CharacterId workerId,
            WorkTypeId workTypeId,
            BuildingInstanceId facilityId)
    {
        if (!workerId.IsValid
            || !workTypeId.IsValid
            || !facilityId.IsValid)
        {
            throw new InvalidOperationException(
                "Work-accident operation allocation requires exact persistent worker, work-type, and facility identities.");
        }

        // Work orders and accident operations share the already-persisted
        // monotonic work sequence. Accident allocations intentionally leave
        // harmless gaps in visible work-order numbers.
        WorkOrderAggregateState state = WritableState;
        int sequence = state.NextOrderSequence;
        state.NextOrderSequence = checked(sequence + 1);
        return $"work-accident-operation:{sequence:D8}";
    }

    private void BumpWorkOrderCandidates() => stateStore.InvalidateCandidates();

    private WorkOrderAggregateState CurrentState => stateStore.Current;
    private WorkOrderAggregateState WritableState => stateStore.Writable;
    private Dictionary<string, WorkOrderRecord> ordersById =>
        WritableState.OrdersById;

    private static string BuildConstructionDestinationId(string orderId)
    {
        return WorkConstructionInputOwnerAuthority.DestinationFor(orderId);
    }

    private WorkOrderAggregateState BuildRestoredState(
        DungeonWorkOrderSaveData snapshot)
    {
        int nextCandidateVersion = CurrentState.CandidateVersion;
        unchecked
        {
            nextCandidateVersion++;
        }

        WorkOrderAggregateState restored = new WorkOrderAggregateState
        {
            NextOrderSequence = snapshot.nextOrderSequence,
            NextRepairOutcomeSequence = snapshot.nextRepairOutcomeSequence,
            CandidateVersion = nextCandidateVersion
        };
        foreach (WorkOrderSaveData source in snapshot.orders)
        {
            WorkOrderRecord order = FromSaveData(source);
            restored.OrdersById.Add(order.workOrderId, order);
        }
        foreach (QualityTargetPipelineSaveData source in
                 snapshot.qualityPipelines
                    ?? new List<QualityTargetPipelineSaveData>())
        {
            QualityTargetPipelineSaveData pipeline = source.CloneNormalized();
            restored.QualityPipelinesById.Add(pipeline.pipelineId, pipeline);
        }
        foreach (RepairWorkOutcomeSaveData source in
                 snapshot.repairOutcomes
                    ?? new List<RepairWorkOutcomeSaveData>())
        {
            RepairWorkOutcomeSaveData repair = source.Clone();
            restored.RepairOutcomesById.Add(repair.operationId, repair);
        }

        foreach (var row in snapshot.pendingAccidents)
            restored.PendingAccidents.Add(row.fact.operationId, row.Clone());
        return restored;
    }

    private static WorkOrderSaveData ToSaveData(WorkOrderRecord order)
    {
        WorkOrderStatus durableStatus = order.status == WorkOrderStatus.InProgress
            ? WorkOrderStatus.Ready
            : order.status;
        return new WorkOrderSaveData
        {
            workOrderId = order.workOrderId,
            workTypeId = order.workTypeId.Value,
            targetBuildingId = order.targetBuildingId,
            gridX = order.position.x,
            gridY = order.position.y,
            requiredWork = order.requiredWork,
            completedWork = order.completedWork,
            materialDestinationId = order.materialDestinationId,
            constructionSitePersistentId = order.constructionSitePersistentId,
            materialBufferCapacityGrams = order.materialBufferCapacityGrams,
            materialMassAuthorityRevision = order.materialMassAuthorityRevision,
            materialCapacityFingerprint = order.materialCapacityFingerprint,
            reservedWorkerPersistentId = string.Empty,
            workerPolicy = order.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(),
            contributions = order.contributions
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList(),
            qualityRoll = CloneRoll(order.qualityRoll),
            qualityPipelineId = order.qualityPipelineId ?? string.Empty,
            qualityAttemptIndex = Mathf.Max(0, order.qualityAttemptIndex),
            destructiveDrainOperationId =
                order.destructiveDrainOperationId ?? string.Empty,
            facilityRemovedForRetry = order.facilityRemovedForRetry,
            cancelRebuildAfterDestructiveDrain =
                order.cancelRebuildAfterDestructiveDrain,
            status = durableStatus,
            itemMaterials = order.requiredItemMaterials
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new WorkOrderItemMaterialSaveData
                {
                    itemId = pair.Key,
                    required = pair.Value,
                    delivered = order.deliveredItemMaterials.TryGetValue(
                        pair.Key,
                        out int delivered)
                        ? delivered
                        : 0
                })
                .ToList(),
            materialTransfer = WorkOrderMaterialOutbox.ToSaveData(
                order.materialTransfer),
            terminalPhase = order.terminalPhase,
            terminalKind = order.terminalKind,
            terminalFactCaptured = order.terminalFactCaptured,
            terminalFact = order.terminalFact?.Clone(),
            terminalPublicationKey = order.terminalPublicationKey,
            placementOperationId = order.placementOperationId ?? string.Empty,
            supportBuildingIds = order.supportBuildingIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            finalFacilityId = order.finalFacilityId ?? string.Empty,
            finalFacilityDefinitionId =
                order.finalFacilityDefinitionId ?? string.Empty,
            replacedFacilityId = order.replacedFacilityId ?? string.Empty,
            replacedFacilityDefinitionId =
                order.replacedFacilityDefinitionId ?? string.Empty,
            recoveryOutputs = order.requiredRecoveryOutputs
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new WorkOrderItemMaterialSaveData
                {
                    itemId = pair.Key,
                    required = pair.Value,
                    delivered = order.spawnedRecoveryOutputs.TryGetValue(
                        pair.Key,
                        out int spawned)
                        ? spawned
                        : 0
                })
                .ToList()
        };
    }

    private static WorkOrderRecord FromSaveData(WorkOrderSaveData source)
    {
        WorkTypeCatalog.TryGet(
            source.workTypeId,
            out WorkTypeDefinition definition);

        WorkOrderRecord order = new WorkOrderRecord
        {
            workOrderId = source.workOrderId ?? string.Empty,
            workTypeId = definition.WorkTypeId,
            targetBuildingId = source.targetBuildingId,
            position = new Vector2Int(source.gridX, source.gridY),
            requiredWork = Mathf.Max(0.1f, source.requiredWork),
            completedWork = Mathf.Clamp(source.completedWork, 0f, Mathf.Max(0.1f, source.requiredWork)),
            preciseCompletedWork = Math.Min(
                Math.Max(0.1d, source.requiredWork),
                Math.Max(0d, source.completedWork)),
            materialDestinationId = source.materialDestinationId ?? string.Empty,
            constructionSitePersistentId =
                source.constructionSitePersistentId ?? string.Empty,
            materialBufferCapacityGrams = source.materialBufferCapacityGrams,
            materialMassAuthorityRevision = source.materialMassAuthorityRevision,
            materialCapacityFingerprint =
                source.materialCapacityFingerprint ?? string.Empty,
            reservedWorkerPersistentId = source.reservedWorkerPersistentId ?? string.Empty,
            workerPolicy = source.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(),
            qualityRoll = CloneRoll(source.qualityRoll),
            qualityPipelineId = source.qualityPipelineId?.Trim() ?? string.Empty,
            qualityAttemptIndex = Mathf.Max(0, source.qualityAttemptIndex),
            destructiveDrainOperationId =
                source.destructiveDrainOperationId ?? string.Empty,
            facilityRemovedForRetry = source.facilityRemovedForRetry,
            cancelRebuildAfterDestructiveDrain =
                source.cancelRebuildAfterDestructiveDrain,
            status = source.status,
            materialTransfer = WorkOrderMaterialOutbox.FromSaveData(
                source.materialTransfer),
            terminalPhase = source.terminalPhase,
            terminalKind = source.terminalKind,
            terminalFactCaptured = source.terminalFactCaptured,
            terminalFact = source.terminalFact?.Clone(),
            terminalPublicationKey = source.terminalPublicationKey,
            placementOperationId = source.placementOperationId?.Trim()
                ?? string.Empty,
            finalFacilityId = source.finalFacilityId?.Trim() ?? string.Empty,
            finalFacilityDefinitionId =
                source.finalFacilityDefinitionId?.Trim() ?? string.Empty,
            replacedFacilityId =
                source.replacedFacilityId?.Trim() ?? string.Empty,
            replacedFacilityDefinitionId =
                source.replacedFacilityDefinitionId?.Trim() ?? string.Empty
        };

        order.supportBuildingIds.AddRange((source.supportBuildingIds
                ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .OrderBy(value => value, StringComparer.Ordinal));

        order.contributions.AddRange((source.contributions
                ?? new List<CraftContributionSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone()));

        foreach (WorkOrderItemMaterialSaveData material
                 in source.itemMaterials
                    ?? new List<WorkOrderItemMaterialSaveData>())
        {
            string itemId = material?.itemId?.Trim() ?? string.Empty;
            if (itemId.Length == 0 || material.required <= 0)
            {
                continue;
            }

            order.requiredItemMaterials[itemId] = material.required;
            order.deliveredItemMaterials[itemId] = Mathf.Clamp(
                material.delivered,
                0,
                material.required);
        }

        foreach (WorkOrderItemMaterialSaveData material
                 in source.recoveryOutputs
                    ?? new List<WorkOrderItemMaterialSaveData>())
        {
            string itemId = material?.itemId?.Trim() ?? string.Empty;
            if (itemId.Length == 0 || material.required <= 0)
            {
                continue;
            }
            order.requiredRecoveryOutputs[itemId] = material.required;
            order.spawnedRecoveryOutputs[itemId] = Mathf.Clamp(
                material.delivered,
                0,
                material.required);
        }

        return order;
    }

    private static WorkOrderProgressState ToProgressState(WorkOrderRecord order)
    {
        return new WorkOrderProgressState
        {
            WorkOrderId = order.workOrderId,
            WorkTypeId = order.workTypeId,
            TargetBuildingId = order.targetBuildingId,
            Position = order.position,
            RequiredWork = order.requiredWork,
            CompletedWork = order.completedWork,
            MaterialDestinationId = order.materialDestinationId,
            ReservedWorkerPersistentId = order.reservedWorkerPersistentId,
            WorkerPolicy = order.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(),
            Contributions = order.contributions
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToArray(),
            QualityRoll = CloneRoll(order.qualityRoll),
            QualityPipelineId = order.qualityPipelineId ?? string.Empty,
            QualityAttemptIndex = Mathf.Max(0, order.qualityAttemptIndex),
            Status = order.status,
            ItemMaterialRequirements = new Dictionary<string, int>(
                order.requiredItemMaterials,
                StringComparer.Ordinal),
            DeliveredItemMaterials = new Dictionary<string, int>(
                order.deliveredItemMaterials,
                StringComparer.Ordinal),
            MaterialTransferPhase = order.materialTransfer.Phase,
            MaterialInputMassGrams = order.materialTransfer.InputMassGrams
        };
    }

    private T ResolveOptional<T>() where T : class
    {
        return objectResolver.TryResolve(typeof(T), out object resolved)
            ? resolved as T
            : null;
    }

    private void RefreshWorkerAvailability()
    {
        if (characterWorld == null)
        {
            return;
        }
        bool changed = false;
        foreach (WorkOrderRecord order in ordersById.Values)
        {
            if (order.status is WorkOrderStatus.Completed
                or WorkOrderStatus.Cancelled
                or WorkOrderStatus.WaitingForMaterials
                or WorkOrderStatus.Blocked)
            {
                continue;
            }
            if (order.status == WorkOrderStatus.TargetCurrentlyUnreachable)
            {
                if (WritableState.QualityPipelinesById.TryGetValue(
                        order.qualityPipelineId ?? string.Empty,
                        out QualityTargetPipelineSaveData qualityPipeline)
                    && CanPotentiallyReachQuality(
                        order,
                        qualityPipeline.minimumQuality))
                {
                    order.status = WorkOrderStatus.WaitingForMaterials;
                    qualityPipeline.stage =
                        QualityTargetPipelineStage.WaitingForMaterials;
                    materialSupply.RequestMissingMaterials(order);
                    changed = true;
                }
                continue;
            }
            bool available = characterWorld.Characters.Any(actor =>
                actor != null
                && WorkerSelectionPolicyRules.IsEligible(
                    order.workerPolicy,
                    actor,
                    narrativeQualifications,
                    out _));
            if (!available)
            {
                if (order.status != WorkOrderStatus.WaitingForEligibleWorker)
                {
                    itemStackRuntime.ReleaseStacksByDestination(
                        order.materialDestinationId,
                        order.position);
                    order.status = WorkOrderStatus.WaitingForEligibleWorker;
                    changed = true;
                }
            }
            else if (order.status == WorkOrderStatus.WaitingForEligibleWorker)
            {
                order.status = WorkOrderStatus.WaitingForMaterials;
                materialSupply.RequestMissingMaterials(order);
                changed = true;
            }
        }
        if (changed)
        {
            BumpWorkOrderCandidates();
        }
    }

    private BuildableObject ApplyCompletedBuildingQuality(
        WorkOrderRecord order,
        CraftQualityResolution quality)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return null;
        }
        BuildableObject completed = grid.GetGridCell(order.position)?
            .GetAllOccupants()
            .OfType<BuildableObject>()
            .FirstOrDefault(value => value != null
                && value is not ConstructionSite
                && value.id == order.targetBuildingId);
        completed?.Craftsmanship.Configure(
            quality,
            order.qualityPipelineId,
            order.qualityAttemptIndex);
        return completed;
    }

    private void ResolveQualityPipelineAttempt(
        WorkOrderRecord order,
        CraftQualityResolution quality,
        BuildableObject completedBuilding)
    {
        string pipelineId = order?.qualityPipelineId?.Trim() ?? string.Empty;
        if (pipelineId.Length == 0
            || !WritableState.QualityPipelinesById.TryGetValue(
                pipelineId,
                out QualityTargetPipelineSaveData pipeline))
        {
            return;
        }

        pipeline.consumedWork += Mathf.Max(0f, order.requiredWork);
        pipeline.attemptIndex = Mathf.Max(
            pipeline.attemptIndex,
            order.qualityAttemptIndex + 1);
        if ((int)quality.Tier >= (int)pipeline.minimumQuality)
        {
            pipeline.acceptedCount++;
            pipeline.stage = pipeline.acceptedCount >= pipeline.requiredAcceptedCount
                ? QualityTargetPipelineStage.Completed
                : QualityTargetPipelineStage.WaitingForMaterials;
            return;
        }

        if (pipeline.HasReachedSafeLimit)
        {
            pipeline.stage = QualityTargetPipelineStage.Paused;
            return;
        }

        pipeline.stage = pipeline.rejectedDisposition switch
        {
            RejectedOutputDisposition.DismantleFacilityAndRetry =>
                QualityTargetPipelineStage.Dismantling,
            RejectedOutputDisposition.KeepFacilityAndStop =>
                QualityTargetPipelineStage.Paused,
            _ => QualityTargetPipelineStage.Paused
        };
        pipeline.currentRoll = qualityResolver.Roll(
            unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
            pipeline.pipelineId,
            pipeline.definitionId,
            pipeline.attemptIndex);
        if (pipeline.stage == QualityTargetPipelineStage.Dismantling)
        {
            CreateFacilityDismantleOrder(
                pipeline,
                completedBuilding,
                order.requiredWork);
        }
    }

    private void CreateFacilityDismantleOrder(
        QualityTargetPipelineSaveData pipeline,
        BuildableObject completedBuilding,
        float originalConstructionWork)
    {
        if (pipeline == null || completedBuilding == null)
        {
            if (pipeline != null)
            {
                pipeline.stage = QualityTargetPipelineStage.Paused;
            }
            return;
        }

        BuildingSO building = completedBuilding.BuildingData
            ?? TryGetBuilding(completedBuilding.id);
        if (building == null || salvageCalculator == null)
        {
            pipeline.stage = QualityTargetPipelineStage.Paused;
            return;
        }

        MaterialSalvageResult estimate = salvageCalculator.Calculate(
            ResolveFacilityDismantleKind(building),
            originalConstructionWork,
            building.GetConstructionMaterials(),
            workerSkill: 50f);
        WorkOrderRecord dismantle = new WorkOrderRecord
        {
            workOrderId = NextOrderId(),
            workTypeId = BuiltInWorkTypeIds.Dismantle,
            targetBuildingId = building.id,
            position = completedBuilding.centerPos,
            requiredWork = estimate.RequiredWork,
            completedWork = 0f,
            preciseCompletedWork = 0d,
            materialDestinationId = $"quality-recovery:{pipeline.pipelineId}",
            workerPolicy = pipeline.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(
                    WorkerCandidateSortMode.BestExpectedQuality),
            qualityPipelineId = pipeline.pipelineId,
            qualityAttemptIndex = Mathf.Max(0, pipeline.attemptIndex),
            qualityRoll = CloneRoll(pipeline.currentRoll),
            status = WorkOrderStatus.Ready
        };
        ordersById.Add(dismantle.workOrderId, dismantle);
        RuntimeWorkCapabilityMarker marker =
            completedBuilding.GetComponent<RuntimeWorkCapabilityMarker>()
            ?? completedBuilding.gameObject
                .AddComponent<RuntimeWorkCapabilityMarker>();
        marker.Add(pipeline.pipelineId, BuiltInWorkTypeIds.Dismantle);
        facilityCandidateCache?.Clear();
        BumpWorkOrderCandidates();
        workforceReplanService?.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.Dismantle,
            forceInterrupt: false);
    }

    private bool CompleteFacilityDismantle(
        WorkOrderRecord order,
        BuildableObject target,
        float weightedSkill,
        out bool appliedCompletionEffects,
        out string message)
    {
        appliedCompletionEffects = false;
        message = string.Empty;
        if (order == null
            || !WritableState.QualityPipelinesById.TryGetValue(
                order.qualityPipelineId ?? string.Empty,
                out QualityTargetPipelineSaveData pipeline))
        {
            message = "품질 파이프라인을 찾을 수 없습니다.";
            return false;
        }

        BuildingSO building = TryGetBuilding(order.targetBuildingId);
        if (building == null || salvageCalculator == null)
        {
            pipeline.stage = QualityTargetPipelineStage.Paused;
            message = "해체 원본 시설이나 회수 계산기가 없습니다.";
            return false;
        }

        if (!order.facilityRemovedForRetry)
        {
            if (target == null || placementService == null)
            {
                message = "시설 배치 서비스가 준비되지 않았습니다.";
                return false;
            }
            if (!target.PersistentInstanceId.IsValid)
            {
                message = "해체 대상 시설의 영속 ID가 없습니다.";
                return false;
            }

            MaterialSalvageResult salvage = salvageCalculator.Calculate(
                ResolveFacilityDismantleKind(building),
                balanceWorkCalculator?.CalculateConstruction(building)
                    ?? building.GetRequiredWork(BuiltInWorkTypeIds.Construct),
                building.GetConstructionMaterials(),
                weightedSkill > 0f ? weightedSkill : 50f);
            order.requiredRecoveryOutputs.Clear();
            order.spawnedRecoveryOutputs.Clear();
            foreach (ItemAmountDefinition recovered in salvage.RecoveredMaterials)
            {
                order.requiredRecoveryOutputs[recovered.ItemId] =
                    recovered.Amount;
                order.spawnedRecoveryOutputs[recovered.ItemId] = 0;
            }

            ProductionFacilityDestructiveDrainOperationId operationId =
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    target.PersistentInstanceId);
            if (order.destructiveDrainOperationId.Length == 0)
            {
                order.destructiveDrainOperationId = operationId.Value;
            }
            else if (!string.Equals(
                    order.destructiveDrainOperationId,
                    operationId.Value,
                    StringComparison.Ordinal))
            {
                destructiveDrainJoinConflict =
                    "work-order-destructive-drain-operation-conflict:"
                    + order.workOrderId;
                message = destructiveDrainJoinConflict;
                return false;
            }
            if (!TryEnsureDismantleObservation(
                    order,
                    target,
                    operationId,
                    out message))
            {
                return false;
            }

            GridBuildingDestroyRequestResult request =
                placementService.RequestDestroyBuilding(target);
            if (!request.Accepted)
            {
                RemoveDismantleObservation(order.workOrderId);
                order.destructiveDrainOperationId = string.Empty;
                message = request.FailureReason;
                return false;
            }
            pipeline.stage = QualityTargetPipelineStage.Recovering;
            appliedCompletionEffects = true;
            facilityCandidateCache?.Clear();
            if (!request.Removed)
            {
                order.status = WorkOrderStatus.Blocked;
                message = request.FailureReason;
                BumpWorkOrderCandidates();
                return true;
            }
            if (!order.facilityRemovedForRetry)
            {
                destructiveDrainJoinConflict =
                    "work-order-destructive-drain-removal-event-missing:"
                    + order.workOrderId;
                message = destructiveDrainJoinConflict;
                return false;
            }
        }

        bool followUpCompleted = TryFinishFacilityRecoveryAndRebuild(
            order,
            pipeline,
            building,
            out message);
        if (!followUpCompleted && order.facilityRemovedForRetry)
        {
            // The worker-owned dismantle phase is already complete and cannot
            // be rolled back: the facility was removed and recovery outputs
            // may already have been spawned.  Output-space/material/rebuild
            // continuation is an order-owned background phase driven by Tick.
            // Report the accepted labor as completed instead of turning the
            // irreversible success into a failed AI action.
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Facility dismantled; recovery or rebuild is pending.";
            }
            return true;
        }

        return followUpCompleted;
    }

    private void ContinuePendingFacilityRecoveries()
    {
        ReconcilePendingDismantleWorldRemovals();
        foreach (WorkOrderRecord order in ordersById.Values
                     .Where(value => value != null
                         && value.workTypeId == BuiltInWorkTypeIds.Dismantle
                         && value.facilityRemovedForRetry)
                     .ToArray())
        {
            if (!WritableState.QualityPipelinesById.TryGetValue(
                    order.qualityPipelineId ?? string.Empty,
                    out QualityTargetPipelineSaveData pipeline))
            {
                continue;
            }
            BuildingSO building = TryGetBuilding(order.targetBuildingId);
            if (building == null)
                continue;

            if (order.cancelRebuildAfterDestructiveDrain)
            {
                if (TryPublishFacilityRecoveryOutputs(order, out _))
                {
                    order.status = WorkOrderStatus.Completed;
                    RemoveDismantleObservation(order.workOrderId);
                    ordersById.Remove(order.workOrderId);
                    facilityCandidateCache?.Clear();
                    BumpWorkOrderCandidates();
                }
                continue;
            }

            if (pipeline.IsTerminal
                || pipeline.stage == QualityTargetPipelineStage.Paused)
            {
                continue;
            }
            TryFinishFacilityRecoveryAndRebuild(
                order,
                pipeline,
                building,
                out _);
        }
    }

    private bool TryEnsureDismantleObservation(
        WorkOrderRecord order,
        BuildableObject building,
        ProductionFacilityDestructiveDrainOperationId operationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || building == null || !operationId.IsValid)
        {
            failureReason =
                "work-order-destructive-drain-observation-invalid";
            return false;
        }
        if (pendingDismantleObservations.TryGetValue(
                order.workOrderId,
                out PendingDismantleObservation existing))
        {
            if (ReferenceEquals(existing.Building, building)
                && existing.OperationId.Equals(operationId))
            {
                return true;
            }
            failureReason =
                "work-order-destructive-drain-observation-conflict:"
                + order.workOrderId;
            return false;
        }

        Action handler = null;
        handler = () => AcknowledgeDismantleWorldRemoval(
            order.workOrderId,
            operationId);
        pendingDismantleObservations.Add(
            order.workOrderId,
            new PendingDismantleObservation(
                order.workOrderId,
                building,
                operationId,
                handler));
        building.OnBuildingDestroyed += handler;
        return true;
    }

    private void AcknowledgeDismantleWorldRemoval(
        string orderId,
        ProductionFacilityDestructiveDrainOperationId operationId)
    {
        RemoveDismantleObservation(orderId);
        if (!ordersById.TryGetValue(orderId, out WorkOrderRecord order)
            || !string.Equals(
                order.destructiveDrainOperationId,
                operationId.Value,
                StringComparison.Ordinal))
        {
            destructiveDrainJoinConflict =
                "work-order-destructive-drain-event-owner-conflict:"
                + orderId;
            Debug.LogError(destructiveDrainJoinConflict);
            return;
        }
        if (order.facilityRemovedForRetry)
            return;
        order.facilityRemovedForRetry = true;
        BumpWorkOrderCandidates();
    }

    private void ReconcilePendingDismantleWorldRemovals()
    {
        if (destructiveDrainJoinConflict.Length > 0)
            return;
        foreach (WorkOrderRecord order in ordersById.Values
                     .Where(value => value != null
                         && value.workTypeId == BuiltInWorkTypeIds.Dismantle
                         && !value.facilityRemovedForRetry
                         && !string.IsNullOrEmpty(
                             value.destructiveDrainOperationId))
                     .OrderBy(value => value.workOrderId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!ProductionFacilityDestructiveDrainOperationId.TryParse(
                    order.destructiveDrainOperationId,
                    out ProductionFacilityDestructiveDrainOperationId operationId))
            {
                SetDestructiveDrainJoinConflict(
                    "work-order-destructive-drain-operation-invalid:",
                    order);
                return;
            }
            BuildingInstanceId facilityId = (BuildingInstanceId)
                operationId.Value.Substring(
                    ProductionFacilityDestructiveDrainOperationId.Prefix.Length);
            DismantleFacilityResolution resolution =
                ResolveDismantleFacility(
                    order,
                    facilityId,
                    out BuildableObject live);
            if (resolution == DismantleFacilityResolution.Unavailable)
                continue;
            if (resolution == DismantleFacilityResolution.Conflict)
            {
                SetDestructiveDrainJoinConflict(
                    "work-order-destructive-drain-world-duplicate:",
                    order);
                return;
            }
            if (resolution == DismantleFacilityResolution.Present)
            {
                if (!TryEnsureDismantleObservation(
                        order,
                        live,
                        operationId,
                        out string observationFailure))
                {
                    SetDestructiveDrainJoinConflict(
                        observationFailure + ":",
                        order);
                    return;
                }
                continue;
            }

            IProductionFacilityDestructiveDrainJournalQuery journal =
                destructiveDrainJournal();
            if (journal == null
                || !journal.TryGet(
                    operationId,
                    out ProductionFacilityDestructiveDrainEntrySaveData entry)
                || entry == null
                || entry.cause !=
                    ProductionFacilityDestructiveDrainCause.ExplicitDemolition
                || !string.Equals(
                    entry.facilityId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            {
                SetDestructiveDrainJoinConflict(
                    "work-order-destructive-drain-terminal-join-missing:",
                    order);
                return;
            }
            if (entry.phase != ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc)
            {
                SetDestructiveDrainJoinConflict(
                    "work-order-destructive-drain-world-missing-before-terminal:",
                    order);
                return;
            }
            order.facilityRemovedForRetry = true;
            BumpWorkOrderCandidates();
        }
    }

    private enum DismantleFacilityResolution
    {
        Unavailable,
        Absent,
        Present,
        Conflict
    }

    private DismantleFacilityResolution ResolveDismantleFacility(
        WorkOrderRecord order,
        BuildingInstanceId facilityId,
        out BuildableObject building)
    {
        building = null;
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
            return DismantleFacilityResolution.Unavailable;
        GridCell cell = grid.GetGridCell(order.position);
        if (cell == null)
            return DismantleFacilityResolution.Absent;
        BuildableObject[] matches = cell.GetAllOccupants()
            .OfType<BuildableObject>()
            .Where(value => value != null
                && !value.isDestroy
                && value.id == order.targetBuildingId
                && value.PersistentInstanceId.Equals(facilityId))
            .Take(2)
            .ToArray();
        if (matches.Length == 0)
            return DismantleFacilityResolution.Absent;
        if (matches.Length > 1)
            return DismantleFacilityResolution.Conflict;
        building = matches[0];
        return DismantleFacilityResolution.Present;
    }

    private void SetDestructiveDrainJoinConflict(
        string prefix,
        WorkOrderRecord order)
    {
        destructiveDrainJoinConflict = prefix + order.workOrderId;
        order.status = WorkOrderStatus.Blocked;
        Debug.LogError(destructiveDrainJoinConflict);
    }

    private void RemoveDismantleObservation(string orderId)
    {
        if (!pendingDismantleObservations.Remove(
                orderId,
                out PendingDismantleObservation pending))
        {
            return;
        }
        if (pending.Building != null)
            pending.Building.OnBuildingDestroyed -= pending.Handler;
    }

    private void ClearPendingDismantleObservations()
    {
        foreach (PendingDismantleObservation pending in
                 pendingDismantleObservations.Values)
        {
            if (pending?.Building != null)
                pending.Building.OnBuildingDestroyed -= pending.Handler;
        }
        pendingDismantleObservations.Clear();
    }

    private bool TryFinishFacilityRecoveryAndRebuild(
        WorkOrderRecord order,
        QualityTargetPipelineSaveData pipeline,
        BuildingSO building,
        out string message)
    {
        message = string.Empty;
        if (!TryPublishFacilityRecoveryOutputs(order, out message))
        {
            pipeline.stage = QualityTargetPipelineStage.WaitingForOutputSpace;
            return false;
        }

        bool hasRequiredReserve = materialSupply.HasRequiredMaterialReserve(
            pipeline,
            building);
        pipeline.stage = QualityTargetPipelineStage.Rebuilding;
        if (!hasRequiredReserve && placementService == null)
        {
            pipeline.stage = QualityTargetPipelineStage.Paused;
            order.status = WorkOrderStatus.WaitingForMaterials;
            message = "다음 재건이 지정 최소 비축량을 침범합니다.";
            return false;
        }
        if (placementService == null
            || !placementService.TryPlaceQualityRetryConstructionSite(
                building,
                order.position,
                out string constructionOrderId,
                out message)
            || !ordersById.TryGetValue(
                constructionOrderId,
                out WorkOrderRecord construction))
        {
            order.status = WorkOrderStatus.WaitingForOutputSpace;
            return false;
        }

        construction.qualityPipelineId = pipeline.pipelineId;
        construction.qualityAttemptIndex = pipeline.attemptIndex;
        construction.qualityRoll = CloneRoll(pipeline.currentRoll);
        construction.workerPolicy = pipeline.workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
        if (!hasRequiredReserve)
        {
            itemStackRuntime.ReleaseStacksByDestination(
                construction.materialDestinationId,
                construction.position);
            construction.status = WorkOrderStatus.WaitingForMaterials;
            pipeline.stage = QualityTargetPipelineStage.Paused;
            message = "The next rebuild would cross the configured material reserve.";
        }
        order.status = WorkOrderStatus.Completed;
        RemoveDismantleObservation(order.workOrderId);
        ordersById.Remove(order.workOrderId);
        facilityCandidateCache?.Clear();
        BumpWorkOrderCandidates();
        return true;
    }

    private bool TryPublishFacilityRecoveryOutputs(
        WorkOrderRecord order,
        out string message)
    {
        message = string.Empty;
        foreach (KeyValuePair<string, int> pair in
                 order.requiredRecoveryOutputs
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            int spawned = order.spawnedRecoveryOutputs.TryGetValue(
                pair.Key,
                out int current)
                ? current
                : 0;
            int remaining = pair.Value - spawned;
            if (remaining <= 0)
            {
                continue;
            }
            if (!itemStackRuntime.SpawnItemAt(
                    pair.Key,
                    remaining,
                    order.position,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int created))
            {
                created = Mathf.Max(0, created);
            }
            order.spawnedRecoveryOutputs[pair.Key] =
                Mathf.Min(pair.Value, spawned + created);
            if (created < remaining)
            {
                order.status = WorkOrderStatus.WaitingForOutputSpace;
                message = "해체 회수품을 놓을 안전한 공간이 없습니다.";
                return false;
            }
        }
        return true;
    }

    private static DismantleTargetKind ResolveFacilityDismantleKind(
        BuildingSO building)
    {
        ConstructionBalanceClass balanceClass =
            V23BalanceWorkCalculator.ResolveConstructionClass(building);
        return balanceClass == ConstructionBalanceClass.Arcane
            ? DismantleTargetKind.ArcaneFacility
            : balanceClass is ConstructionBalanceClass.Precision
                or ConstructionBalanceClass.Industrial
                ? DismantleTargetKind.PrecisionIndustrialFacility
                : DismantleTargetKind.GeneralFacility;
    }

    private bool CanPotentiallyReachQuality(
        WorkOrderRecord order,
        CraftsmanshipQualityTier minimumQuality)
    {
        if (characterWorld == null)
        {
            return true;
        }
        float bestSkill = -1f;
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (actor != null
                && WorkerSelectionPolicyRules.IsEligible(
                    order.workerPolicy,
                    actor,
                    narrativeQualifications,
                    out _))
            {
                bestSkill = Mathf.Max(
                    bestSkill,
                    GetConstructionQualitySkill(actor));
            }
        }
        if (bestSkill < 0f)
        {
            return true;
        }
        CraftQualityResolution theoreticalBest = qualityResolver.Resolve(
            new CraftQualityRollSaveData
            {
                attemptIndex = order.qualityAttemptIndex,
                randomA = 10,
                randomB = 10,
                randomC = 10
            },
            bestSkill,
            facilityBonus: 0f,
            toolBonus: 0f,
            complexityPenalty: ResolveConstructionComplexityPenalty(order));
        return (int)theoreticalBest.Tier >= (int)minimumQuality;
    }

    private bool TryGetMutablePipeline(
        string pipelineId,
        out QualityTargetPipelineSaveData pipeline,
        out DomainFailure failure)
    {
        pipeline = null;
        failure = DomainFailure.None;
        string normalized = pipelineId?.Trim() ?? string.Empty;
        if (normalized.Length == 0
            || !WritableState.QualityPipelinesById.TryGetValue(
                normalized,
                out pipeline))
        {
            failure = new DomainFailure(FailureCode.QualityPipelineBlocked);
            return false;
        }
        return true;
    }

    private bool ChangePipelineStage(
        string pipelineId,
        QualityTargetPipelineStage stage,
        bool allowTerminal,
        out DomainFailure failure)
    {
        if (!TryGetMutablePipeline(
                pipelineId,
                out QualityTargetPipelineSaveData pipeline,
                out failure)
            || (!allowTerminal && pipeline.IsTerminal))
        {
            return false;
        }
        pipeline.stage = stage;
        BumpWorkOrderCandidates();
        return true;
    }

    private float GetConstructionQualitySkill(CharacterActor worker)
    {
        if (worker == null) return 25f;
        if (performance == null)
            throw new InvalidOperationException(
                "Construction quality requires the character performance query.");
        CharacterPerformanceSnapshot snapshot = performance.Evaluate(
            worker,
            "performance:work:construct:quality");
        if (!snapshot.IsApplicable)
            throw new InvalidOperationException(
                snapshot.Failure?.Message ?? "Construction quality is unavailable.");
        return Mathf.Clamp(snapshot.Value * 58f, 0f, 100f);
    }

    private static float ResolveConstructionComplexityPenalty(WorkOrderRecord order)
    {
        int materialKinds = order?.requiredItemMaterials.Count ?? 0;
        return materialKinds <= 2 ? 0f : materialKinds <= 4 ? 4f : 8f;
    }

    private static CraftQualityRollSaveData CloneRoll(
        CraftQualityRollSaveData source) => source == null
        ? null
        : new CraftQualityRollSaveData
        {
            attemptIndex = source.attemptIndex,
            randomA = source.randomA,
            randomB = source.randomB,
            randomC = source.randomC
        };
}
