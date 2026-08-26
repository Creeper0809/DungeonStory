using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionConsumerDemandTarget
{
    public string RuntimeConsumerId { get; set; } = string.Empty;
    public string DestinationId { get; set; } = string.Empty;
    public Vector2Int DestinationPosition { get; set; }
    public int DemandQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int ReservationLimit { get; set; }
    public string BlockedReason { get; set; } = string.Empty;
    public bool RoutingOwnedExternally { get; set; }
}

public readonly struct ProductionConsumerDemandContext
{
    public ProductionConsumerDemandContext(
        string itemId,
        ProductionConsumerLink link)
    {
        ItemId = itemId?.Trim() ?? string.Empty;
        Link = link ?? throw new ArgumentNullException(nameof(link));
    }

    public string ItemId { get; }
    public ProductionConsumerLink Link { get; }
}

public interface IProductionConsumerDemandProvider
{
    bool Supports(ProductionConsumerKind kind);
    void Collect(
        ProductionConsumerDemandContext context,
        ICollection<ProductionConsumerDemandTarget> destination);
}

public interface IProductionDistributionQuery
{
    IReadOnlyList<ProductionConsumerRouteState> GetRouteStates(
        ProductionBillId billId);
}

// Compatibility probe retained for the assembly-local delivery-coordinator
// regression. Runtime distribution no longer calls this helper; all production
// routing goes through IProductionPreparedOutputExactRouteLifecycle.
internal static class ProductionPreparedOutputDeliveryDispatch
{
    internal static bool RequiresCompletedAuthority(
        ProductionPreparedOutputRouteRequestSnapshot route) =>
        route.Phase == ProductionPreparedOutputRoutePhase
            .ItemsAcknowledgedAwaitingCheckpointGc
        && (route.CurrentDeliveryTargetKind ==
                ProductionPreparedOutputDeliveryTargetKind
                    .WarehouseSelectionPending
            || string.IsNullOrEmpty(route.CurrentTargetAuthorityFingerprint));

    internal static bool TryApply(
        IProductionPreparedOutputDeliveryCoordinator coordinator,
        ProductionPreparedOutputRouteRequestSnapshot route)
    {
        if (coordinator == null)
            throw new ArgumentNullException(nameof(coordinator));
        if (string.IsNullOrEmpty(route.RouteOperationId))
        {
            throw new InvalidOperationException(
                "Prepared-output delivery route identity is missing.");
        }

        return (string.IsNullOrEmpty(route.CurrentTargetDestinationId)
                ? coordinator.TryApplyCompatibleWarehouse(
                    route.RouteOperationId,
                    route.ItemId,
                    route.CurrentTargetPositionX,
                    route.CurrentTargetPositionY)
                : coordinator.TryApplyExactTarget(
                    route.RouteOperationId,
                    ProductionPreparedOutputDeliveryRerouteReason
                        .InitialTargetAuthorityConfirmed,
                    route.CurrentTargetDestinationId,
                    route.CurrentTargetPositionX,
                    route.CurrentTargetPositionY))
            .Succeeded;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionDistributionRuntime :
    IProductionDistributionQuery,
    ITickable
{
    private const int MaximumRoutesPerTick = 16;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionDependencyCatalog dependencies;
    private readonly IReadOnlyList<IProductionConsumerDemandProvider> providers;
    private readonly IProductionAssemblyBridge bridge;
    private readonly IProductionPreparedOutputRoutingAuthority preparedRouting;
    private readonly IProductionPreparedOutputExactRouteLifecycle routeLifecycle;
    private readonly IGameClock clock;

    public ProductionDistributionRuntime(
        ProductionAggregateStateStore stateStore,
        IResourceEconomyContentCatalog catalog,
        IProductionDependencyCatalog dependencies,
        IReadOnlyList<IProductionConsumerDemandProvider> providers,
        IProductionAssemblyBridge bridge,
        IProductionPreparedOutputRoutingAuthority preparedRouting,
        IProductionPreparedOutputExactRouteLifecycle routeLifecycle,
        IGameClock clock)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
        this.providers = providers
            ?? throw new ArgumentNullException(nameof(providers));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.preparedRouting = preparedRouting
            ?? throw new ArgumentNullException(nameof(preparedRouting));
        this.routeLifecycle = routeLifecycle
            ?? throw new ArgumentNullException(nameof(routeLifecycle));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        bool changed = false;
        int routedCount = 0;
        foreach (ProductionBillRecord bill in stateStore.Bills
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.billId.Value, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!catalog.TryGetRecipe(bill.recipeId, out ProductionRecipeSO recipe))
            {
                continue;
            }
            changed |= EnsurePolicies(bill, recipe);
            HashSet<string> selectedConsumerIds = new(StringComparer.Ordinal);
            ProductionFacilityHandle facility = ResolveFacility(bill.buildingInstanceId);
            if (ProductionPreparedOutputMigrationScope.Contains(bill.recipeId))
            {
                if (routedCount < MaximumRoutesPerTick
                    && TryProgressPreparedOutputRoute(
                        bill,
                        facility,
                        selectedConsumerIds))
                {
                    routedCount++;
                    changed = true;
                }
                changed |= UpdateWaitingTimes(
                    bill,
                    recipe,
                    selectedConsumerIds,
                    clock.DeltaTime);
                continue;
            }
            if (facility != null)
            {
                foreach (IGrouping<string, ProductionOutputDefinition> output in
                         recipe.Outputs
                             .Where(value => value != null && value.Amount > 0)
                             .GroupBy(value => value.ItemId, StringComparer.Ordinal))
                {
                    if (routedCount >= MaximumRoutesPerTick)
                    {
                        break;
                    }
                    int buffered = bridge.CountBufferedOutput(
                        output.Key,
                        bill.outputDestinationId);
                    if (buffered <= 0)
                    {
                        continue;
                    }
                    int quantum = Mathf.Max(1, output.Sum(value => value.Amount));
                    if (TryRouteOne(
                            bill,
                            facility,
                            output.Key,
                            Mathf.Min(buffered, quantum),
                            selectedConsumerIds))
                    {
                        routedCount++;
                        changed = true;
                    }
                }
            }
            changed |= UpdateWaitingTimes(
                bill,
                recipe,
                selectedConsumerIds,
                clock.DeltaTime);
        }

        if (changed)
        {
            unchecked
            {
                stateStore.IncrementBillVersion();
            }
        }
    }

    public IReadOnlyList<ProductionConsumerRouteState> GetRouteStates(
        ProductionBillId billId)
    {
        ProductionBillRecord record = stateStore.Bills.FirstOrDefault(
            candidate => candidate != null
                && candidate.billId.Equals(billId));
        if (record == null
            || !catalog.TryGetRecipe(
                record.recipeId,
                out ProductionRecipeSO recipe))
        {
            return Array.Empty<ProductionConsumerRouteState>();
        }

        IReadOnlyList<ProductionConsumerRoutePolicy> policies =
            GetEffectivePolicies(record, recipe);
        List<ProductionConsumerRouteState> result = new();
        foreach (ProductionConsumerRoutePolicy policy in policies)
        {
            ProductionConsumerRouteState[] itemStates = recipe.Outputs
                .Where(output => output != null)
                .Select(output => BuildStateForItem(
                    output.ItemId,
                    policy,
                    FindLink(output.ItemId, policy.consumerId)))
                .Where(state => state != null)
                .ToArray();
            if (itemStates.Length == 0)
            {
                continue;
            }
            ProductionConsumerRouteState active = itemStates
                .Where(state => string.IsNullOrWhiteSpace(state.blockedReason)
                    && state.currentDemand > state.reservedQuantity)
                .OrderBy(state => state.stage)
                .FirstOrDefault();
            ProductionConsumerRouteState first = active ?? itemStates[0];
            result.Add(new ProductionConsumerRouteState
            {
                policy = policy.Clone(),
                kind = first.kind,
                displayName = first.displayName,
                destinationId = first.destinationId,
                currentDemand = itemStates.Sum(state => state.currentDemand),
                reservedQuantity = itemStates.Sum(state => state.reservedQuantity),
                reservationLimit = itemStates.Sum(state => state.reservationLimit),
                activeConsumerCount = itemStates.Sum(
                    state => state.activeConsumerCount),
                stage = first.stage,
                blockedReason = active != null
                    ? string.Empty
                    : string.Join(
                        ",",
                        itemStates
                            .Select(state => state.blockedReason)
                            .Where(reason => !string.IsNullOrWhiteSpace(reason))
                            .Distinct(StringComparer.Ordinal))
            });
        }
        return result;
    }

    private bool TryRouteOne(
        ProductionBillRecord bill,
        ProductionFacilityHandle facility,
        string itemId,
        int amount,
        ISet<string> selectedConsumerIds)
    {
        List<RouteEvaluation> evaluations = BuildEvaluations(bill, itemId);
        ProductionConsumerRoutePolicy selected =
            ProductionDistributionPlanner.SelectNext(
                bill.distributionMode,
                evaluations.Select(value => value.State));
        if (selected != null)
        {
            RouteEvaluation evaluation = evaluations.First(value =>
                string.Equals(
                    value.State.policy.consumerId,
                    selected.consumerId,
                    StringComparison.Ordinal));
            ProductionConsumerDemandTarget target = SelectTarget(evaluation);
            int shortage = Mathf.Max(
                0,
                evaluation.State.currentDemand
                    - evaluation.State.reservedQuantity);
            int targetCapacity = target == null
                ? 0
                : evaluation.State.stage
                    == ProductionDistributionStage.ActiveDemand
                    ? Mathf.Max(
                        0,
                        target.ReservationLimit
                            - target.ReservedQuantity)
                    : shortage;
            int routedAmount = Mathf.Min(amount, Mathf.Min(shortage, targetCapacity));
            if (target != null && routedAmount > 0)
            {
                bool completed = bridge.TryRouteBufferedOutput(
                    bill.outputDestinationId,
                    itemId,
                    routedAmount,
                    target.DestinationPosition,
                    target.DestinationId,
                    out int routed,
                    out _);
                if (routed > 0)
                {
                    selectedConsumerIds.Add(selected.consumerId);
                    return true;
                }
                if (!completed)
                {
                    return false;
                }
            }
        }

        if (HasCompatibleWarehouse(itemId))
        {
            return bridge.TryRouteBufferedOutput(
                bill.outputDestinationId,
                itemId,
                amount,
                facility.Position,
                string.Empty,
                out int routed,
                out DomainFailure failure)
                && routed == amount;
        }

        if (facility.AllowsOverflowDump)
        {
            return bridge.TryRouteBufferedOutput(
                bill.outputDestinationId,
                itemId,
                amount,
                facility.Position + facility.OverflowOffset,
                string.Empty,
                out int routed,
                out DomainFailure failure)
                && routed == amount;
        }

        return false;
    }

    private bool TryProgressPreparedOutputRoute(
        ProductionBillRecord bill,
        ProductionFacilityHandle facility,
        ISet<string> selectedConsumerIds)
    {
        ProductionPreparedOutputRoutingLineSnapshot[] lines = preparedRouting
            .CaptureBill(bill.billId)
            .OrderBy(value => value.CycleSequence)
            .ThenBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ThenBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (lines.Length == 0)
        {
            return false;
        }

        HashSet<string> lineCommitIds = lines
            .Select(value => value.LineCommitId)
            .ToHashSet(StringComparer.Ordinal);
        ProductionPreparedOutputRouteRequestSnapshot[] operations = preparedRouting
            .CaptureRouteOperations()
            .Where(value => lineCommitIds.Contains(value.LineCommitId))
            .OrderBy(value => value.RouteOperationId, StringComparer.Ordinal)
            .ToArray();
        ProductionPreparedOutputRouteRequestSnapshot pendingAuthority = operations
            .Where(value => value.Phase == ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc
                && (value.CurrentDeliveryTargetKind ==
                        ProductionPreparedOutputDeliveryTargetKind
                            .WarehouseSelectionPending
                    || string.IsNullOrEmpty(
                        value.CurrentTargetAuthorityFingerprint)))
            .FirstOrDefault();
        if (!string.IsNullOrEmpty(pendingAuthority.RouteOperationId))
        {
            return routeLifecycle.TryProgress(pendingAuthority).Completed;
        }
        ProductionPreparedOutputRouteRequestSnapshot pending = operations
            .Where(value => value.Phase != ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc)
            .FirstOrDefault();
        if (!string.IsNullOrEmpty(pending.RouteOperationId))
        {
            return routeLifecycle.TryProgress(pending).Completed;
        }

        foreach (ProductionPreparedOutputRoutingLineSnapshot line in lines
                     .Where(value => value.RemainingQuantity > 0
                         && value.RemainingMassGrams > 0L))
        {
            if (!TrySelectPreparedRouteTarget(
                    bill,
                    facility,
                    line,
                    out string targetDestinationId,
                    out Vector2Int targetPosition,
                    out int requestedQuantity,
                    out string selectedConsumerId))
            {
                continue;
            }

            int exactQuantity = routeLifecycle.ResolveExactQuantity(
                line,
                requestedQuantity);
            if (exactQuantity <= 0
                && targetDestinationId.Length > 0
                && HasCompatibleWarehouse(line.ItemId))
            {
                targetDestinationId = string.Empty;
                targetPosition = facility?.Position ?? default;
                exactQuantity = routeLifecycle.ResolveExactQuantity(
                    line,
                    line.RemainingQuantity);
                selectedConsumerId = string.Empty;
            }
            if (exactQuantity <= 0)
            {
                continue;
            }

            ProductionPreparedOutputRouteRequestSnapshot operation =
                preparedRouting.PrepareRoute(
                    line.BatchCommitId,
                    line.LineCommitId,
                    targetDestinationId,
                    targetPosition.x,
                    targetPosition.y,
                    exactQuantity);
            bool routed = routeLifecycle.TryProgress(operation).Completed;
            if (routed && !string.IsNullOrEmpty(selectedConsumerId))
            {
                selectedConsumerIds.Add(selectedConsumerId);
            }
            return routed;
        }

        return false;
    }

    private bool TrySelectPreparedRouteTarget(
        ProductionBillRecord bill,
        ProductionFacilityHandle facility,
        ProductionPreparedOutputRoutingLineSnapshot line,
        out string targetDestinationId,
        out Vector2Int targetPosition,
        out int requestedQuantity,
        out string selectedConsumerId)
    {
        targetDestinationId = string.Empty;
        targetPosition = facility?.Position ?? default;
        requestedQuantity = 0;
        selectedConsumerId = string.Empty;

        List<RouteEvaluation> evaluations = BuildEvaluations(
            bill,
            line.ItemId);
        ProductionConsumerRoutePolicy selected =
            ProductionDistributionPlanner.SelectNext(
                bill.distributionMode,
                evaluations.Select(value => value.State));
        if (selected != null)
        {
            RouteEvaluation evaluation = evaluations.First(value =>
                string.Equals(
                    value.State.policy.consumerId,
                    selected.consumerId,
                    StringComparison.Ordinal));
            ProductionConsumerDemandTarget target = SelectTarget(evaluation);
            int shortage = Mathf.Max(
                0,
                evaluation.State.currentDemand
                    - evaluation.State.reservedQuantity);
            int targetCapacity = target == null
                ? 0
                : evaluation.State.stage
                    == ProductionDistributionStage.ActiveDemand
                    ? Mathf.Max(
                        0,
                        target.ReservationLimit - target.ReservedQuantity)
                    : shortage;
            int amount = Mathf.Min(
                line.RemainingQuantity,
                Mathf.Min(shortage, targetCapacity));
            if (target != null
                && amount > 0
                && !string.IsNullOrEmpty(target.DestinationId))
            {
                targetDestinationId = target.DestinationId;
                targetPosition = target.DestinationPosition;
                requestedQuantity = amount;
                selectedConsumerId = selected.consumerId;
                return true;
            }
        }

        if (!HasCompatibleWarehouse(line.ItemId))
        {
            return false;
        }

        requestedQuantity = line.RemainingQuantity;
        return requestedQuantity > 0;
    }

    private List<RouteEvaluation> BuildEvaluations(
        ProductionBillRecord bill,
        string itemId)
    {
        List<RouteEvaluation> result = new();
        foreach (ProductionConsumerRoutePolicy policy in bill.routePolicies)
        {
            ProductionConsumerLink link = FindLink(itemId, policy.consumerId);
            if (link == null)
            {
                continue;
            }
            List<ProductionConsumerDemandTarget> targets = CollectTargets(
                itemId,
                link);
            result.Add(new RouteEvaluation(
                BuildStateForItem(itemId, policy, link, targets),
                targets));
        }
        return result;
    }

    private ProductionConsumerRouteState BuildStateForItem(
        string itemId,
        ProductionConsumerRoutePolicy policy,
        ProductionConsumerLink link)
    {
        return link == null
            ? null
            : BuildStateForItem(
                itemId,
                policy,
                link,
                CollectTargets(itemId, link));
    }

    private static ProductionConsumerRouteState BuildStateForItem(
        string itemId,
        ProductionConsumerRoutePolicy policy,
        ProductionConsumerLink link,
        IReadOnlyList<ProductionConsumerDemandTarget> targets)
    {
        ProductionConsumerDemandTarget[] usable = (targets
                ?? Array.Empty<ProductionConsumerDemandTarget>())
            .Where(target => target != null
                && string.IsNullOrWhiteSpace(target.BlockedReason))
            .ToArray();
        ProductionConsumerDemandTarget[] counted = usable.Length > 0
            ? usable
            : (targets ?? Array.Empty<ProductionConsumerDemandTarget>())
                .Where(target => target != null)
                .ToArray();
        int activeDemand = counted.Sum(target => target.DemandQuantity);
        int reserved = counted.Sum(target => target.ReservedQuantity);
        int limit = counted.Sum(target => target.ReservationLimit);
        int desired = activeDemand;
        ProductionDistributionStage stage =
            ProductionDistributionStage.ActiveDemand;
        if (desired <= reserved && policy.minimumReserve > reserved)
        {
            desired = policy.minimumReserve;
            stage = ProductionDistributionStage.MinimumReserve;
        }
        if (desired <= reserved && policy.targetStock > reserved)
        {
            desired = policy.targetStock;
            stage = ProductionDistributionStage.TargetStock;
        }
        limit = Mathf.Max(limit, desired);

        string blocked = string.Empty;
        if (!policy.enabled)
        {
            blocked = "route-disabled";
        }
        else if (targets == null || targets.Count == 0)
        {
            blocked = "inactive-consumer";
        }
        else if (usable.Length == 0)
        {
            blocked = string.Join(
                ",",
                targets
                    .Select(target => target?.BlockedReason)
                    .Where(reason => !string.IsNullOrWhiteSpace(reason))
                    .Distinct(StringComparer.Ordinal));
        }
        else if (desired <= reserved)
        {
            blocked = "demand-satisfied";
        }
        else if (limit > 0 && reserved >= limit)
        {
            blocked = "reservation-cap-reached";
        }

        return new ProductionConsumerRouteState
        {
            policy = policy.Clone(),
            kind = link.kind,
            displayName = string.IsNullOrWhiteSpace(link.displayName)
                ? link.consumerId
                : link.displayName,
            destinationId = usable.Length == 1
                ? usable[0].DestinationId
                : usable.Length > 1
                    ? $"multiple:{usable.Length}"
                    : string.Empty,
            currentDemand = Mathf.Max(0, desired),
            reservedQuantity = Mathf.Max(0, reserved),
            reservationLimit = Mathf.Max(0, limit),
            activeConsumerCount = usable.Length,
            stage = stage,
            blockedReason = blocked
        };
    }

    private List<ProductionConsumerDemandTarget> CollectTargets(
        string itemId,
        ProductionConsumerLink link)
    {
        List<ProductionConsumerDemandTarget> result = new();
        IProductionConsumerDemandProvider[] matching = providers
            .Where(provider => provider != null
                && provider.Supports(link.kind))
            .ToArray();
        foreach (IProductionConsumerDemandProvider provider in matching)
        {
            provider.Collect(
                new ProductionConsumerDemandContext(itemId, link),
                result);
        }
        if (matching.Length == 0)
        {
            result.Add(new ProductionConsumerDemandTarget
            {
                RuntimeConsumerId = link.consumerId,
                BlockedReason = "consumer-provider-unavailable"
            });
        }
        return result;
    }

    private static ProductionConsumerDemandTarget SelectTarget(
        RouteEvaluation evaluation)
    {
        if (evaluation == null)
        {
            return null;
        }
        return evaluation.Targets
            .Where(target => target != null
                && !target.RoutingOwnedExternally
                && string.IsNullOrWhiteSpace(target.BlockedReason)
                && (evaluation.State.stage
                        != ProductionDistributionStage.ActiveDemand
                    || target.ReservedQuantity < target.ReservationLimit))
            .OrderBy(target =>
                evaluation.State.stage == ProductionDistributionStage.ActiveDemand
                    ? target.ReservedQuantity >= target.DemandQuantity
                    : false)
            .ThenBy(target => target.ReservedQuantity)
            .ThenBy(target => target.RuntimeConsumerId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private bool EnsurePolicies(
        ProductionBillRecord bill,
        ProductionRecipeSO recipe)
    {
        HashSet<string> authored = recipe.Outputs
            .Where(output => output != null)
            .SelectMany(output => dependencies.GetConsumers(output.ItemId))
            .Where(link => link != null && link.IsRealConsumer)
            .Select(link => link.consumerId)
            .ToHashSet(StringComparer.Ordinal);
        bool changed = bill.RemoveRoutes(policy => policy == null
            || !authored.Contains(policy.consumerId)) > 0;
        HashSet<string> existing = bill.routePolicies
            .Select(policy => policy.consumerId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string consumerId in authored.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (existing.Add(consumerId))
            {
                bill.AddRoute(new ProductionConsumerRoutePolicy
                {
                    consumerId = consumerId,
                    enabled = true,
                    priority = 50,
                    weight = 1
                });
                changed = true;
            }
        }
        return changed;
    }

    private IReadOnlyList<ProductionConsumerRoutePolicy> GetEffectivePolicies(
        ProductionBillRecord bill,
        ProductionRecipeSO recipe)
    {
        if (bill.routePolicies.Count > 0)
        {
            return bill.routePolicies;
        }
        return recipe.Outputs
            .Where(output => output != null)
            .SelectMany(output => dependencies.GetConsumers(output.ItemId))
            .Where(link => link != null && link.IsRealConsumer)
            .GroupBy(link => link.consumerId, StringComparer.Ordinal)
            .Select(group => new ProductionConsumerRoutePolicy
            {
                consumerId = group.Key,
                enabled = true,
                priority = 50,
                weight = 1
            })
            .ToArray();
    }

    private bool UpdateWaitingTimes(
        ProductionBillRecord bill,
        ProductionRecipeSO recipe,
        ISet<string> selected,
        float deltaTime)
    {
        bool changed = false;
        IReadOnlyDictionary<string, ProductionConsumerRouteState> states =
            GetRouteStates(bill.billId)
                .ToDictionary(
                    state => state.policy.consumerId,
                    StringComparer.Ordinal);
        foreach (ProductionConsumerRoutePolicy policy in bill.routePolicies)
        {
            float previous = policy.waitingSeconds;
            if (selected.Contains(policy.consumerId))
            {
                policy.waitingSeconds = 0f;
            }
            else if (states.TryGetValue(
                         policy.consumerId,
                         out ProductionConsumerRouteState state)
                     && policy.enabled
                     && string.IsNullOrWhiteSpace(state.blockedReason)
                     && state.currentDemand > state.reservedQuantity)
            {
                policy.waitingSeconds = Mathf.Min(
                    86400f,
                    policy.waitingSeconds + Mathf.Max(0f, deltaTime));
            }
            if (!Mathf.Approximately(previous, policy.waitingSeconds))
            {
                changed = true;
            }
        }
        return changed;
    }

    private ProductionConsumerLink FindLink(string itemId, string consumerId)
    {
        return dependencies.GetConsumers(itemId).FirstOrDefault(link =>
            link != null
            && string.Equals(
                link.consumerId,
                consumerId,
                StringComparison.Ordinal));
    }

    private ProductionFacilityHandle ResolveFacility(BuildingInstanceId id)
    {
        return bridge.Facilities.FirstOrDefault(building => building != null
            && !building.IsDestroyed
            && building.InstanceId.Equals(id));
    }

    private bool HasCompatibleWarehouse(string itemId)
    {
        return catalog.TryGetItem(itemId, out ResourceItemDefinitionSO item)
            && bridge.HasCompatibleWarehouse(itemId, item.StockCategory);
    }

    private sealed class RouteEvaluation
    {
        public RouteEvaluation(
            ProductionConsumerRouteState state,
            IReadOnlyList<ProductionConsumerDemandTarget> targets)
        {
            State = state;
            Targets = targets ?? Array.Empty<ProductionConsumerDemandTarget>();
        }

        public ProductionConsumerRouteState State { get; }
        public IReadOnlyList<ProductionConsumerDemandTarget> Targets { get; }
    }
}
