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
    CropTreatment = 13,
    SocietyEvent = 14
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
public enum ProductionOutputRole
{
    Main = 0,
    Byproduct = 1,
    ReturnedPackaging = 2,
    RecoverableWaste = 3,
    DeclaredLoss = 4,
    DeclaredExternalInput = 5
}

public static class ProductionOutputRoleRules
{
    public static bool IsNonPhysical(ProductionOutputRole role) =>
        role is ProductionOutputRole.DeclaredLoss
            or ProductionOutputRole.DeclaredExternalInput;

    public static bool IsPhysical(ProductionOutputRole role) =>
        Enum.IsDefined(typeof(ProductionOutputRole), role)
        && !IsNonPhysical(role);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionOutputDefinition
{
    [SerializeField] private string outputLineId = string.Empty;
    [SerializeField] private ProductionOutputRole role;
    [SerializeField] private string itemId = string.Empty;
    [Min(1), SerializeField] private int amount = 1;
    [Range(0f, 1f), SerializeField] private float probability = 1f;

    public string OutputLineId => outputLineId ?? string.Empty;
    public ProductionOutputRole Role => role;
    public string ItemId => itemId?.Trim() ?? string.Empty;
    public int Amount => Mathf.Max(1, amount);
    public float Probability => Mathf.Clamp01(probability);
    public bool HasCanonicalAuthoredValue =>
        IsCanonicalOutputLineId(outputLineId)
        && Enum.IsDefined(typeof(ProductionOutputRole), role)
        && !string.IsNullOrWhiteSpace(itemId)
        && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
        && amount > 0
        && !float.IsNaN(probability)
        && !float.IsInfinity(probability)
        && probability >= 0f
        && probability <= 1f;

    public ProductionOutputDefinition()
    {
    }

    public ProductionOutputDefinition(
        string outputLineId,
        ProductionOutputRole role,
        string itemId,
        int amount,
        float probability = 1f)
    {
        if (!IsCanonicalOutputLineId(outputLineId))
        {
            throw new ArgumentException(
                "Production output line IDs must be non-empty canonical "
                + "ASCII IDs beginning with 'output:'.",
                nameof(outputLineId));
        }
        if (!Enum.IsDefined(typeof(ProductionOutputRole), role))
            throw new ArgumentOutOfRangeException(nameof(role), role, null);

        this.outputLineId = outputLineId;
        this.role = role;
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(1, amount);
        this.probability = Mathf.Clamp01(probability);
    }

    public static bool IsCanonicalOutputLineId(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !value.StartsWith("output:", StringComparison.Ordinal)
            || value.Length == "output:".Length
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool allowed = character >= 'a' && character <= 'z'
                || character >= '0' && character <= '9'
                || character == ':' || character == '/'
                || character == '.' || character == '_'
                || character == '-';
            if (!allowed)
                return false;
        }

        return true;
    }
}
