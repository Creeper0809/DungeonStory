using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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

public sealed class WorkOrderRuntime :
    IWorkOrderRuntime,
    IWorkOrderQuery,
    ITickable,
    IDungeonRestoreTransactionParticipant
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("WorkOrderRuntime.Tick");

    public const string ConstructionDestinationPrefix = "construction:";

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IBuildingDefinitionLookup buildingDefinitionLookup;
    private readonly IObjectResolver objectResolver;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IGameClock gameClock;
    private readonly IUiClock uiClock;
    private readonly WorkOrderAggregateStateStore stateStore;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly Dictionary<ConstructionSite, string> orderIdBySite =
        new Dictionary<ConstructionSite, string>();
    private List<WorkOrderConstructionSiteRestoreCandidate> stagedSiteCandidates;
    private PublishedWorkOrderSites publishedSites;
    private bool restoreTransactionActive;
    private bool restoreCandidatePrepared;
    private float nextReadyConstructionReplanAt;

    public string ParticipantId => "150.world.construction-sites";
    public int WorkOrderCandidateVersion => CurrentState.CandidateVersion;
    public int Version => CurrentState.CandidateVersion;
    public IReadOnlyList<WorkOrderProgressState> ActiveOrders => ordersById.Values
        .Where(order => order != null
            && order.status != WorkOrderStatus.Completed
            && order.status != WorkOrderStatus.Cancelled)
        .OrderBy(order => order.workOrderId, StringComparer.Ordinal)
        .Select(ToProgressState)
        .ToArray();

    public WorkOrderRuntime(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IBuildingDefinitionLookup buildingDefinitionLookup,
        IObjectResolver objectResolver,
        WorkOrderExecutionServices executionServices,
        WorkOrderAggregateStateStore stateStore)
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
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
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
        float cadenceTime = uiClock.Time;
        if (gameClock.IsPaused || cadenceTime < nextReadyConstructionReplanAt)
        {
            return;
        }

        nextReadyConstructionReplanAt = cadenceTime + 1f;
        string orphanedConstructionOrderId = ordersById.Values
            .Where(order => order.workTypeId == BuiltInWorkTypeIds.Construct
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

        if (!ordersById.Values.Any(order =>
                order.workTypeId == BuiltInWorkTypeIds.Construct
                && order.status == WorkOrderStatus.Ready
                && !HasAssignedConstructionWorker(order.workOrderId)))
        {
            return;
        }

        workforceReplanService.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.Construct,
            forceInterrupt: true);
    }

    public DungeonWorkOrderSaveData Capture()
    {
        WorkOrderAggregateState state = CurrentState;
        return new DungeonWorkOrderSaveData
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = Mathf.Max(1, state.NextOrderSequence),
            orders = state.OrdersById.Values
                .Where(order => order.status != WorkOrderStatus.Completed && order.status != WorkOrderStatus.Cancelled)
                .OrderBy(order => order.workOrderId, StringComparer.Ordinal)
                .Select(ToSaveData)
                .ToList()
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
        orderId = string.Empty;
        failureReason = string.Empty;
        if (site == null || building == null)
        {
            failureReason = "construction target missing";
            return false;
        }

        WorkOrderRecord order = new WorkOrderRecord
        {
            workOrderId = NextOrderId(),
            workTypeId = BuiltInWorkTypeIds.Construct,
            targetBuildingId = building.id,
            position = position,
            requiredWork = Mathf.Max(0.1f, building.GetRequiredWork(BuiltInWorkTypeIds.Construct)),
            completedWork = 0f,
            materialDestinationId = BuildConstructionDestinationId(building, position),
            status = WorkOrderStatus.WaitingForMaterials
        };

        string installationKitItemId =
            FacilityInstallationKitItemIds.ForBuilding(building);
        if (HasAvailableInstallationKit(installationKitItemId))
        {
            order.requiredItemMaterials[installationKitItemId] = 1;
            order.deliveredItemMaterials[installationKitItemId] = 0;
        }
        else
        {
            foreach (ItemAmountDefinition material
                     in building.GetConstructionMaterials())
            {
                order.requiredItemMaterials.Add(
                    material.ItemId,
                    material.Amount);
                order.deliveredItemMaterials.Add(material.ItemId, 0);
            }
        }

        if (order.requiredItemMaterials.Count == 0)
        {
            order.status = WorkOrderStatus.Ready;
        }
        else
        {
            RequestMissingMaterials(order);
        }

        ordersById[order.workOrderId] = order;
        orderIdBySite[site] = order.workOrderId;
        site.ConfigureWorkOrderRuntime(this);
        orderId = order.workOrderId;
        BumpWorkOrderCandidates();
        if (debugRules.IsEnabled(DungeonDebugCheat.InstantConstruction))
        {
            foreach (string itemId in order.requiredItemMaterials.Keys.ToArray())
            {
                order.deliveredItemMaterials[itemId] =
                    order.requiredItemMaterials[itemId];
            }
            order.status = WorkOrderStatus.Ready;
            CompleteOrder(order, site, out _, out failureReason);
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

        WorkTypeDefinition definition = WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition resolvedDefinition)
            ? resolvedDefinition
            : null;
        string displayName = definition?.DisplayName ?? workTypeId.ToString();
        if (order.status == WorkOrderStatus.WaitingForMaterials && !EnsureMaterialsReady(order, out message))
        {
            RequestMissingMaterials(order);
            return false;
        }

        if (order.status == WorkOrderStatus.Blocked
            || order.status == WorkOrderStatus.Cancelled
            || order.status == WorkOrderStatus.Completed)
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
        order.completedWork = Mathf.Clamp(
            order.completedWork + Mathf.Max(0f, amount),
            0f,
            Mathf.Max(0.1f, order.requiredWork));

        if (order.completedWork + 0.001f < order.requiredWork)
        {
            message = $"{displayName} {Mathf.RoundToInt(order.completedWork / Mathf.Max(0.1f, order.requiredWork) * 100f)}%";
            return true;
        }

        completed = CompleteOrder(order, target, out appliedCompletionEffects, out message);
        return completed;
    }

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

        bool ready = EnsureMaterialsReady(order, out _);
        if (!ready)
        {
            RequestMissingMaterials(order);
        }
        else
        {
            BumpWorkOrderCandidates();
            workforceReplanService?.RequestOneWorkerToReplanFor(
                BuiltInWorkTypeIds.Construct,
                forceInterrupt: true);
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

        order.status = WorkOrderStatus.Cancelled;
        if (refundDeliveredMaterials)
        {
            itemStackRuntime.ReleaseStacksByDestination(
                order.materialDestinationId,
                order.position);
        }
        else
        {
            itemStackRuntime.RemoveStacksByStateAndDestination(
                WorldItemStackState.Loose,
                order.materialDestinationId);
            itemStackRuntime.RemoveStacksByStateAndDestination(
                WorldItemStackState.FacilityBuffer,
                order.materialDestinationId);
            itemStackRuntime.RemoveStacksByStateAndDestination(
                WorldItemStackState.Stored,
                order.materialDestinationId);
        }
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

    private bool HasAssignedConstructionWorker(string orderId)
    {
        foreach (KeyValuePair<ConstructionSite, string> pair in orderIdBySite)
        {
            if (string.Equals(pair.Value, orderId, StringComparison.Ordinal)
                && pair.Key != null
                && pair.Key.ActiveWorker != null)
            {
                return true;
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

        BuildableObject target = ResolveTarget(order);
        if (target == null)
        {
            message = "작업 대상을 찾을 수 없습니다.";
            return false;
        }

        order.completedWork = order.requiredWork;
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
        order.status = WorkOrderStatus.Completed;
        order.completedWork = order.requiredWork;
        ordersById.Remove(order.workOrderId);
        BumpWorkOrderCandidates();

        if (target is ConstructionSite site)
        {
            orderIdBySite.Remove(site);
            appliedCompletionEffects = true;
            bool placed = site.CompleteConstruction();
            message = placed ? "construction completed" : "construction completion failed";
            return placed;
        }

        message = "work completed";
        return true;
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

    private bool EnsureMaterialsReady(WorkOrderRecord order, out string failureReason)
    {
        failureReason = string.Empty;
        if (order.requiredItemMaterials.Count == 0)
        {
            order.status = WorkOrderStatus.Ready;
            return true;
        }

        Dictionary<string, int> missingItems =
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> pair in order.requiredItemMaterials)
        {
            order.deliveredItemMaterials.TryGetValue(
                pair.Key,
                out int delivered);
            int remaining = pair.Value - delivered;
            if (remaining > 0)
            {
                missingItems[pair.Key] = remaining;
            }
        }

        if (missingItems.Count > 0)
        {
            if (!itemStackRuntime.TryConsumeFacilityItemBuffer(
                    order.materialDestinationId,
                    missingItems,
                    out failureReason))
            {
                order.status = WorkOrderStatus.WaitingForMaterials;
                return false;
            }

            foreach (KeyValuePair<string, int> pair in missingItems)
            {
                order.deliveredItemMaterials[pair.Key] =
                    order.deliveredItemMaterials.TryGetValue(
                        pair.Key,
                        out int current)
                        ? current + pair.Value
                        : pair.Value;
            }
        }

        order.status = WorkOrderStatus.Ready;
        BumpWorkOrderCandidates();
        return true;
    }

    private void RequestMissingMaterials(WorkOrderRecord order)
    {
        if (order == null
            || order.requiredItemMaterials.Count == 0)
        {
            return;
        }

        bool requestedAny = false;
        foreach (KeyValuePair<string, int> pair in order.requiredItemMaterials)
        {
            int delivered = order.deliveredItemMaterials.TryGetValue(
                pair.Key,
                out int currentDelivered)
                ? currentDelivered
                : 0;
            int pending = CountPendingDestinationItem(order, pair.Key);
            int remaining = Mathf.Max(
                0,
                pair.Value - delivered - pending);
            if (remaining <= 0)
            {
                continue;
            }

            itemStackRuntime.TryRequestItemDelivery(
                pair.Key,
                remaining,
                order.position,
                order.materialDestinationId,
                out int requested,
                out _);
            requestedAny |= requested > 0;
        }

        if (requestedAny)
        {
            foreach (WorldItemStackSnapshot stack in itemStackRuntime.GetAllStacks())
            {
                if (stack != null
                    && string.Equals(
                        stack.DestinationId,
                        order.materialDestinationId,
                        StringComparison.Ordinal))
                {
                    itemStackRuntime.PrioritizeHaul(stack.StackId);
                }
            }

            workforceReplanService?.RequestOneHaulerToReplan(
                forceInterrupt: true);
        }
    }

    private int CountPendingDestinationItem(
        WorkOrderRecord order,
        string itemId)
    {
        return itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal)
                && (stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.FacilityBuffer
                    || (stack.State == WorldItemStackState.Stored
                        && !string.IsNullOrWhiteSpace(
                            stack.SourceStorageDestinationId))))
            .Sum(stack => stack.Quantity);
    }

    private bool HasAvailableInstallationKit(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        return itemStackRuntime.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && string.IsNullOrWhiteSpace(stack.ReservedByPersistentId)
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
            && ((stack.State == WorldItemStackState.Loose
                    && string.IsNullOrWhiteSpace(stack.DestinationId))
                || stack.State == WorldItemStackState.Stored));
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
                (BuildingInstanceId)$"building:construction:{order.workOrderId}");
            site.transform.position = grid.GetWorldPos(order.position);
            site.SetGrid(grid);
            site.Initialization(building, order.position);
            site.ConfigureWorkOrderRuntime(this);
            site.ConfigureSite(
                order.workOrderId,
                () => TryPlaceFinalBuildingOnRestore(
                    grid,
                    building,
                    order.position),
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

    private bool TryPlaceFinalBuildingOnRestore(Grid grid, BuildingSO building, Vector2Int position)
    {
        GridBuildingPlacementService service = new GridBuildingPlacementService(
            grid,
            null,
            TryGetBuilding);
        return service.TryPlaceBuildingImmediateUnchecked(
            building,
            position,
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

    private void BumpWorkOrderCandidates()
    {
        unchecked
        {
            WritableState.CandidateVersion++;
        }
    }

    private WorkOrderAggregateState CurrentState => stateStore.Current;
    private WorkOrderAggregateState WritableState => stateStore.Writable;
    private Dictionary<string, WorkOrderRecord> ordersById =>
        WritableState.OrdersById;

    private static string BuildConstructionDestinationId(BuildingSO building, Vector2Int position)
    {
        return $"{ConstructionDestinationPrefix}{building.id}:{position.x}:{position.y}";
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
            CandidateVersion = nextCandidateVersion
        };
        foreach (WorkOrderSaveData source in snapshot.orders)
        {
            WorkOrderRecord order = FromSaveData(source);
            restored.OrdersById.Add(order.workOrderId, order);
        }

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
            reservedWorkerPersistentId = string.Empty,
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
            materialDestinationId = source.materialDestinationId ?? string.Empty,
            reservedWorkerPersistentId = source.reservedWorkerPersistentId ?? string.Empty,
            status = source.status
        };

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
            Status = order.status,
            ItemMaterialRequirements = new Dictionary<string, int>(
                order.requiredItemMaterials,
                StringComparer.Ordinal),
            DeliveredItemMaterials = new Dictionary<string, int>(
                order.deliveredItemMaterials,
                StringComparer.Ordinal)
        };
    }
}
