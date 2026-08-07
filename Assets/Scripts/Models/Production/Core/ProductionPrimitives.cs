using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionOrderMode
{
    RepeatCount = 0,
    MaintainStock = 1,
    RepeatForever = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionConsumerKind
{
    RecipeInput = 0,
    EquipmentMaterial = 1,
    ConstructionMaterial = 2,
    FacilitySupply = 3,
    MedicalProcedure = 4,
    DefenseAmmunition = 5,
    CharacterConsumption = 6,
    Installation = 7,
    EquipmentUse = 8,
    MarketSale = 9,
    LineageTransfer = 10,
    EquipmentProcessing = 11,
    CropSowing = 12,
    CropTreatment = 13
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionDistributionMode
{
    DemandWeighted = 0,
    StrictPriority = 1,
    FixedRatio = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionDistributionStage
{
    ActiveDemand = 0,
    MinimumReserve = 1,
    TargetStock = 2,
    Warehouse = 3,
    Overflow = 4,
    LocalBuffer = 5
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionConsumerLink
{
    public string consumerId = string.Empty;
    public ProductionConsumerKind kind;
    public string requiredResearchId = string.Empty;
    public string displayName = string.Empty;

    public bool IsRealConsumer =>
        !string.IsNullOrWhiteSpace(consumerId)
        && !consumerId.StartsWith("sink:", StringComparison.Ordinal);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionConsumerRoutePolicy
{
    public string consumerId = string.Empty;
    public bool enabled = true;
    [Min(0)] public int minimumReserve;
    [Min(0)] public int targetStock;
    [Range(0, 100)] public int priority = 50;
    [Range(1, 10)] public int weight = 1;
    [Min(0f)] public float waitingSeconds;

    public ProductionConsumerRoutePolicy Clone() =>
        new ProductionConsumerRoutePolicy
        {
            consumerId = consumerId?.Trim() ?? string.Empty,
            enabled = enabled,
            minimumReserve = Mathf.Max(0, minimumReserve),
            targetStock = Mathf.Max(minimumReserve, targetStock),
            priority = Mathf.Clamp(priority, 0, 100),
            weight = Mathf.Clamp(weight, 1, 10),
            waitingSeconds = Mathf.Max(0f, waitingSeconds)
        };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionConsumerRouteState
{
    public ProductionConsumerRoutePolicy policy = new();
    public ProductionConsumerKind kind;
    public string displayName = string.Empty;
    public string destinationId = string.Empty;
    [Min(0)] public int currentDemand;
    [Min(0)] public int reservedQuantity;
    [Min(0)] public int reservationLimit;
    [Min(0)] public int activeConsumerCount;
    public ProductionDistributionStage stage =
        ProductionDistributionStage.ActiveDemand;
    public string blockedReason = string.Empty;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ProductionDistributionPlanner
{
    public static ProductionConsumerRoutePolicy SelectNext(
        ProductionDistributionMode mode,
        IEnumerable<ProductionConsumerRouteState> routes)
    {
        ProductionConsumerRouteState[] candidates = (routes
                ?? Array.Empty<ProductionConsumerRouteState>())
            .Where(route => route?.policy != null
                && route.policy.enabled
                && string.IsNullOrWhiteSpace(route.blockedReason)
                && route.currentDemand > route.reservedQuantity
                && (route.reservationLimit <= 0
                    || route.reservedQuantity < route.reservationLimit))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        ProductionDistributionStage earliestStage =
            (ProductionDistributionStage)candidates
                .Min(route => (int)route.stage);
        candidates = candidates
            .Where(route => route.stage == earliestStage)
            .ToArray();
        IOrderedEnumerable<ProductionConsumerRouteState> ordered = mode switch
        {
            ProductionDistributionMode.StrictPriority => candidates
                .OrderByDescending(route => AgedPriority(route.policy))
                .ThenByDescending(route => route.currentDemand - route.reservedQuantity),
            ProductionDistributionMode.FixedRatio => candidates
                .OrderBy(route => (route.reservedQuantity + 1f)
                    / Mathf.Max(1, route.policy.weight))
                .ThenByDescending(route => AgedPriority(route.policy)),
            _ => candidates
                .OrderByDescending(route =>
                    AgedPriority(route.policy) * 100f
                    + (route.currentDemand - route.reservedQuantity)
                    * Mathf.Max(1, route.policy.weight))
        };
        return ordered
            .ThenBy(route => route.policy.consumerId, StringComparer.Ordinal)
            .First()
            .policy;
    }

    private static float AgedPriority(ProductionConsumerRoutePolicy policy) =>
        Mathf.Clamp(policy.priority, 0, 100)
        + Mathf.Max(0f, policy.waitingSeconds) / 30f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemAmountDefinition
{
    [SerializeField] private string itemId = string.Empty;
    [Min(1), SerializeField] private int amount = 1;

    public string ItemId => itemId?.Trim() ?? string.Empty;
    public int Amount => Mathf.Max(1, amount);
    public bool HasCanonicalAuthoredValue =>
        !string.IsNullOrWhiteSpace(itemId)
        && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
        && amount > 0;

    public ItemAmountDefinition()
    {
    }

    public ItemAmountDefinition(string itemId, int amount)
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(1, amount);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionOutputDefinition
{
    [SerializeField] private string itemId = string.Empty;
    [Min(1), SerializeField] private int amount = 1;
    [Range(0f, 1f), SerializeField] private float probability = 1f;

    public string ItemId => itemId?.Trim() ?? string.Empty;
    public int Amount => Mathf.Max(1, amount);
    public float Probability => Mathf.Clamp01(probability);

    public ProductionOutputDefinition()
    {
    }

    public ProductionOutputDefinition(
        string itemId,
        int amount,
        float probability = 1f)
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(1, amount);
        this.probability = Mathf.Clamp01(probability);
    }
}
