using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ProductionRecipeConsumerDemandProvider :
    IProductionConsumerDemandProvider
{
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IBuildingWorldQuery buildings;
    private readonly IProductionItemGateway items;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IProductionPreparedOutputExecutionPort preparedOutputExecution;
    private readonly IProductionOutputBufferGateway outputBuffer;
    private readonly IProductionAssemblyBridge bridge;

    public ProductionRecipeConsumerDemandProvider(
        ProductionAggregateStateStore stateStore,
        IResourceEconomyContentCatalog catalog,
        IBuildingWorldQuery buildings,
        IProductionItemGateway items,
        IProductionOutputPlanningService outputPlanning,
        IProductionPreparedOutputExecutionPort preparedOutputExecution,
        IProductionOutputBufferGateway outputBuffer,
        IProductionAssemblyBridge bridge)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.outputPlanning = outputPlanning
            ?? throw new ArgumentNullException(nameof(outputPlanning));
        this.preparedOutputExecution = preparedOutputExecution
            ?? throw new ArgumentNullException(nameof(preparedOutputExecution));
        this.outputBuffer = outputBuffer
            ?? throw new ArgumentNullException(nameof(outputBuffer));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public bool Supports(ProductionConsumerKind kind) =>
        kind == ProductionConsumerKind.RecipeInput;

    public void Collect(
        ProductionConsumerDemandContext context,
        ICollection<ProductionConsumerDemandTarget> destination)
    {
        if (destination == null
            || !Supports(context.Link.kind)
            || !catalog.TryGetRecipe(
                context.Link.consumerId,
                out ProductionRecipeSO recipe))
        {
            return;
        }

        int inputPerBatch = recipe.Inputs
            .Where(input => input != null
                && string.Equals(
                    input.ItemId,
                    context.ItemId,
                    StringComparison.Ordinal))
            .Sum(input => input.Amount);
        if (inputPerBatch <= 0)
        {
            return;
        }

        foreach (ProductionBillRecord bill in stateStore.Bills
                     .Where(bill => bill != null
                         && !bill.suspended
                         && string.Equals(
                             bill.recipeId,
                             context.Link.consumerId,
                             StringComparison.Ordinal))
                     .OrderBy(bill => bill.billId.Value, StringComparer.Ordinal))
        {
            BuildableObject facility = buildings.Buildings.FirstOrDefault(
                candidate => candidate != null
                    && !candidate.IsGridDestroyed
                    && candidate.PersistentInstanceId.Equals(
                        bill.buildingInstanceId));
            int batches = Mathf.Max(1, bill.prefetchBatchCount);
            if (bill.mode == ProductionOrderMode.RepeatCount)
            {
                if (bill.remainingCycles <= 0)
                {
                    continue;
                }
                batches = Mathf.Min(batches, bill.remainingCycles);
            }
            string blockedReason = facility == null
                ? "consumer-facility-missing"
                : ResolveBlockedReason(
                    bill,
                    recipe,
                    facility,
                    context.ItemId,
                    batches);
            AddTarget(
                destination,
                context.ItemId,
                bill.billId.Value,
                bill.materialDestinationId,
                facility?.centerPos ?? default,
                inputPerBatch * batches,
                blockedReason);
        }
    }

    private string ResolveBlockedReason(
        ProductionBillRecord bill,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        string routedItemId,
        int batches)
    {
        foreach (IGrouping<string, ItemAmountDefinition> input in recipe.Inputs
                     .Where(value => value != null
                         && !string.Equals(
                             value.ItemId,
                             routedItemId,
                             StringComparison.Ordinal))
                     .GroupBy(value => value.ItemId, StringComparer.Ordinal))
        {
            int required = input.Sum(value => value.Amount) * batches;
            int pending = items.CountPending(
                input.Key,
                bill.materialDestinationId);
            if (pending < required
                && items.CountAvailableStock(
                    input.Key,
                    bill.materialDestinationId)
                    + outputBuffer.CountBufferedOutput(input.Key) <= 0)
            {
                return $"other-material-missing:{input.Key}";
            }
        }

        if (ProductionPreparedOutputCapabilitySelection
            .UsesPreparedOutputMaterializer(recipe, bridge))
        {
            if (ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(bill))
            {
                return "prepared-output-legacy-authority-conflict";
            }
            ProductionPreparedOutputCapacityResult capacity =
                preparedOutputExecution.AssessCurrentCapacity(
                    bill,
                    recipe,
                    bridge.CaptureFacility(facility));
            if (!capacity.IsValid)
            {
                return "prepared-output-capacity-invalid-result";
            }
            return capacity.CanBeginCycle
                ? string.Empty
                : capacity.Failure.IsFailure
                    ? "prepared-output-capacity-blocked:"
                        + capacity.Failure.Code
                    : "prepared-output-capacity-invalid-result";
        }

        Dictionary<string, int> otherReservations = stateStore.Bills
            .Where(candidate => candidate != null
                && candidate != bill
                && string.Equals(
                    candidate.outputDestinationId,
                    bill.outputDestinationId,
                    StringComparison.Ordinal))
            .SelectMany(candidate => candidate.outputReservations)
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.Value),
                StringComparer.Ordinal);
        return outputPlanning.HasCapacity(
                recipe,
                bridge.CaptureFacility(facility),
                bill.outputDestinationId,
                otherReservations,
                bill.outputReservations.Count > 0,
                out _)
            ? string.Empty
            : "consumer-output-full";
    }

    private void AddTarget(
        ICollection<ProductionConsumerDemandTarget> destination,
        string itemId,
        string runtimeId,
        string destinationId,
        Vector2Int position,
        int demand,
        string blockedReason)
    {
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (normalizedDestination.Length == 0)
        {
            blockedReason = "consumer-destination-missing";
        }
        destination.Add(new ProductionConsumerDemandTarget
        {
            RuntimeConsumerId = runtimeId,
            DestinationId = normalizedDestination,
            DestinationPosition = position,
            DemandQuantity = Mathf.Max(0, demand),
            ReservedQuantity = normalizedDestination.Length == 0
                ? 0
                : items.CountPending(itemId, normalizedDestination),
            ReservationLimit = Mathf.Max(0, demand),
            BlockedReason = blockedReason ?? string.Empty
        });
    }
}

public sealed class ProductionConstructionConsumerDemandProvider :
    IProductionConsumerDemandProvider
{
    private const string Prefix = "construction:";
    private readonly IWorkOrderQuery workOrders;
    private readonly IProductionItemGateway items;
    private readonly IProductionOutputBufferGateway outputBuffer;

    public ProductionConstructionConsumerDemandProvider(
        IWorkOrderQuery workOrders,
        IProductionItemGateway items,
        IProductionOutputBufferGateway outputBuffer)
    {
        this.workOrders = workOrders
            ?? throw new ArgumentNullException(nameof(workOrders));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.outputBuffer = outputBuffer
            ?? throw new ArgumentNullException(nameof(outputBuffer));
    }

    public bool Supports(ProductionConsumerKind kind) =>
        kind == ProductionConsumerKind.ConstructionMaterial;

    public void Collect(
        ProductionConsumerDemandContext context,
        ICollection<ProductionConsumerDemandTarget> destination)
    {
        if (destination == null
            || !Supports(context.Link.kind)
            || !TryParseBuildingId(context.Link.consumerId, out int buildingId))
        {
            return;
        }

        foreach (WorkOrderProgressState order in workOrders.ActiveOrders
                     .Where(order => order != null
                         && order.TargetBuildingId == buildingId
                         && order.ItemMaterialRequirements != null
                         && order.ItemMaterialRequirements.ContainsKey(
                             context.ItemId))
                     .OrderBy(order => order.WorkOrderId, StringComparer.Ordinal))
        {
            int demand = order.ItemMaterialRequirements[context.ItemId];
            string destinationId = order.MaterialDestinationId?.Trim()
                ?? string.Empty;
            string blockedReason = destinationId.Length == 0
                ? "consumer-destination-missing"
                : order.ItemMaterialRequirements
                    .Where(pair => !string.Equals(
                        pair.Key,
                        context.ItemId,
                        StringComparison.Ordinal))
                    .FirstOrDefault(pair =>
                        items.CountPending(pair.Key, destinationId) < pair.Value
                        && items.CountAvailableStock(pair.Key, destinationId)
                            + outputBuffer.CountBufferedOutput(pair.Key) <= 0)
                    .Key;
            destination.Add(new ProductionConsumerDemandTarget
            {
                RuntimeConsumerId = order.WorkOrderId ?? string.Empty,
                DestinationId = destinationId,
                DestinationPosition = order.Position,
                DemandQuantity = Mathf.Max(0, demand),
                ReservedQuantity = destinationId.Length == 0
                    ? 0
                    : items.CountPending(context.ItemId, destinationId),
                ReservationLimit = Mathf.Max(0, demand),
                BlockedReason = destinationId.Length == 0
                    ? blockedReason
                    : string.IsNullOrWhiteSpace(blockedReason)
                        ? string.Empty
                        : $"other-material-missing:{blockedReason}"
            });
        }
    }

    private static bool TryParseBuildingId(string consumerId, out int id)
    {
        id = -1;
        string value = consumerId?.Trim() ?? string.Empty;
        return value.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(value.Substring(Prefix.Length), out id)
            && id >= 0;
    }
}

public sealed class ProductionEquipmentConsumerDemandProvider :
    IProductionConsumerDemandProvider
{
    private const string Prefix = "equipment:";
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IProductionItemGateway items;
    private readonly IProductionOutputBufferGateway outputBuffer;

    public ProductionEquipmentConsumerDemandProvider(
        ICombatEquipmentRuntime equipment,
        IProductionItemGateway items,
        IProductionOutputBufferGateway outputBuffer)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.outputBuffer = outputBuffer
            ?? throw new ArgumentNullException(nameof(outputBuffer));
    }

    public bool Supports(ProductionConsumerKind kind) =>
        kind == ProductionConsumerKind.EquipmentMaterial;

    public void Collect(
        ProductionConsumerDemandContext context,
        ICollection<ProductionConsumerDemandTarget> destination)
    {
        string consumerId = context.Link.consumerId?.Trim() ?? string.Empty;
        if (destination == null
            || !Supports(context.Link.kind)
            || !consumerId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return;
        }
        string definitionId = consumerId.Substring(Prefix.Length);
        if (!equipment.TryGetDefinition(
                definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            return;
        }

        foreach (CombatEquipmentCraftOrderSaveData order in equipment.CraftQueue
                     .Where(order => order != null
                         && !order.materialsReady
                         && string.Equals(
                             order.definitionId,
                             definitionId,
                             StringComparison.Ordinal))
                     .OrderBy(order => order.orderId, StringComparer.Ordinal))
        {
            IReadOnlyDictionary<string, int> requirements = ResolveRequirements(
                definition,
                order);
            int demand = requirements.TryGetValue(
                context.ItemId,
                out int requiredAmount)
                ? requiredAmount
                : 0;
            if (demand <= 0)
            {
                continue;
            }
            string destinationId = order.materialDestinationId?.Trim()
                ?? string.Empty;
            string blockedReason = destinationId.Length == 0
                ? "consumer-destination-missing"
                : requirements
                    .Where(pair => !string.Equals(
                        pair.Key,
                        context.ItemId,
                        StringComparison.Ordinal))
                    .FirstOrDefault(pair =>
                        items.CountPending(pair.Key, destinationId) < pair.Value
                        && items.CountAvailableStock(pair.Key, destinationId)
                            + outputBuffer.CountBufferedOutput(pair.Key) <= 0)
                    .Key;
            destination.Add(new ProductionConsumerDemandTarget
            {
                RuntimeConsumerId = order.orderId ?? string.Empty,
                DestinationId = destinationId,
                DestinationPosition = new Vector2Int(
                    order.destinationX,
                    order.destinationY),
                DemandQuantity = demand,
                ReservedQuantity = destinationId.Length == 0
                    ? 0
                    : items.CountPending(context.ItemId, destinationId),
                ReservationLimit = demand,
                BlockedReason = destinationId.Length == 0
                    ? blockedReason
                    : string.IsNullOrWhiteSpace(blockedReason)
                        ? string.Empty
                        : $"other-material-missing:{blockedReason}"
            });
        }
    }

    private IReadOnlyDictionary<string, int> ResolveRequirements(
        CombatEquipmentDefinitionSO definition,
        CombatEquipmentCraftOrderSaveData order)
    {
        Dictionary<string, int> result = definition.RequiredComponentInputs
            .Where(input => input != null)
            .GroupBy(input => input.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(input => input.Amount),
                StringComparer.Ordinal);
        CraftMaterialDefinitionSO material = equipment
            .GetAllowedMaterials(definition.EquipmentId)
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.MaterialId,
                    order.materialId,
                    StringComparison.Ordinal));
        if (material != null)
        {
            result.TryGetValue(material.ItemId, out int current);
            result[material.ItemId] = current + definition.PrimaryMaterialAmount;
        }
        return result;
    }
}

public sealed class ProductionSurgeryConsumerDemandProvider :
    IProductionConsumerDemandProvider
{
    private const string Prefix = "medical:";
    private readonly ISurgeryOrderDemandQuery surgery;
    private readonly IProductionItemGateway items;
    private readonly IProductionOutputBufferGateway outputBuffer;

    public ProductionSurgeryConsumerDemandProvider(
        ISurgeryOrderDemandQuery surgery,
        IProductionItemGateway items,
        IProductionOutputBufferGateway outputBuffer)
    {
        this.surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.outputBuffer = outputBuffer
            ?? throw new ArgumentNullException(nameof(outputBuffer));
    }

    public bool Supports(ProductionConsumerKind kind) =>
        kind == ProductionConsumerKind.MedicalProcedure;

    public void Collect(
        ProductionConsumerDemandContext context,
        ICollection<ProductionConsumerDemandTarget> destination)
    {
        string consumerId = context.Link.consumerId?.Trim() ?? string.Empty;
        if (destination == null
            || !Supports(context.Link.kind)
            || !consumerId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return;
        }
        string procedureId = consumerId.Substring(Prefix.Length);
        foreach (SurgeryOrder order in surgery.ActiveOrders
                     .Where(order => order != null
                         && order.IsActive
                         && !order.materialsConsumed
                         && string.Equals(
                             order.procedureId,
                             procedureId,
                             StringComparison.Ordinal))
                     .OrderBy(order => order.orderId, StringComparer.Ordinal))
        {
            Dictionary<string, int> requirements = (order.materials
                    ?? new List<SurgicalMaterialRequirement>())
                .Where(requirement => requirement != null
                    && !requirement.optional)
                .GroupBy(
                    requirement => requirement.itemId?.Trim() ?? string.Empty,
                    StringComparer.Ordinal)
                .Where(group => group.Key.Length > 0)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(requirement =>
                        Mathf.Max(1, requirement.quantity)),
                    StringComparer.Ordinal);
            int demand = requirements.TryGetValue(
                context.ItemId,
                out int requiredAmount)
                ? requiredAmount
                : 0;
            if (demand <= 0)
            {
                continue;
            }
            string destinationId = order.materialDestinationId?.Trim()
                ?? string.Empty;
            string blockedReason = destinationId.Length == 0
                ? "consumer-destination-missing"
                : requirements
                    .Where(pair => !string.Equals(
                        pair.Key,
                        context.ItemId,
                        StringComparison.Ordinal))
                    .FirstOrDefault(pair =>
                        items.CountPending(pair.Key, destinationId) < pair.Value
                        && items.CountAvailableStock(pair.Key, destinationId)
                            + outputBuffer.CountBufferedOutput(pair.Key) <= 0)
                    .Key;
            destination.Add(new ProductionConsumerDemandTarget
            {
                RuntimeConsumerId = order.orderId ?? string.Empty,
                DestinationId = destinationId,
                DestinationPosition = new Vector2Int(
                    order.admissionX,
                    order.admissionY),
                DemandQuantity = demand,
                ReservedQuantity = destinationId.Length == 0
                    ? 0
                    : items.CountPending(context.ItemId, destinationId),
                ReservationLimit = demand,
                BlockedReason = destinationId.Length == 0
                    ? blockedReason
                    : string.IsNullOrWhiteSpace(blockedReason)
                        ? string.Empty
                        : $"other-material-missing:{blockedReason}"
            });
        }
    }
}

public sealed class ProductionMarketSaleConsumerDemandProvider :
    IProductionConsumerDemandProvider
{
    private const string ConsumerPrefix = "market-sale:";
    private const string DestinationPrefix = "stock-policy:sell:";

    private readonly IResourceStockPolicyQuery stockPolicies;
    private readonly IProductionItemGateway items;

    public ProductionMarketSaleConsumerDemandProvider(
        IResourceStockPolicyQuery stockPolicies,
        IProductionItemGateway items)
    {
        this.stockPolicies = stockPolicies
            ?? throw new ArgumentNullException(nameof(stockPolicies));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public bool Supports(ProductionConsumerKind kind) =>
        kind == ProductionConsumerKind.MarketSale;

    public void Collect(
        ProductionConsumerDemandContext context,
        ICollection<ProductionConsumerDemandTarget> destination)
    {
        string consumerId = context.Link.consumerId?.Trim() ?? string.Empty;
        if (destination == null
            || !Supports(context.Link.kind)
            || !consumerId.StartsWith(ConsumerPrefix, StringComparison.Ordinal)
            || !string.Equals(
                consumerId.Substring(ConsumerPrefix.Length),
                context.ItemId,
                StringComparison.Ordinal))
        {
            return;
        }

        ResourceStockPolicyData policy = stockPolicies.Policies
            .FirstOrDefault(candidate => candidate != null
                && candidate.enabled
                && candidate.surplusDisposition == StockSurplusDisposition.Sell
                && string.Equals(
                    candidate.itemId,
                    context.ItemId,
                    StringComparison.Ordinal));
        if (policy == null)
        {
            return;
        }

        int demand = Mathf.Max(
            0,
            stockPolicies.CountOwned(context.ItemId) - policy.maximumStock);
        if (demand <= 0)
        {
            return;
        }

        string destinationId = DestinationPrefix + context.ItemId;
        destination.Add(new ProductionConsumerDemandTarget
        {
            RuntimeConsumerId = consumerId,
            DestinationId = destinationId,
            DemandQuantity = demand,
            ReservedQuantity = items.CountPending(
                context.ItemId,
                destinationId),
            ReservationLimit = demand,
            // ResourceStockPolicyRuntime owns market hauling and settlement.
            // Distribution exposes its live demand but must not issue a
            // second delivery command for the same physical stock.
            RoutingOwnedExternally = true
        });
    }
}
