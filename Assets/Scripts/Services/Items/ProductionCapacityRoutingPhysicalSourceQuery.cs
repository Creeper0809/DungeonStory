using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Immutable live projection of every physical descendant of one prepared
/// output batch. It is the only bridge allowed to turn world/carry state into
/// the frozen capacity-routing drain source vector.
/// </summary>
public sealed class ProductionCapacityRoutingPhysicalSourceSnapshot
{
    public ProductionCapacityRoutingPhysicalSourceSnapshot(
        string batchCommitId,
        string sourceDestinationId,
        Vector2Int originPosition,
        IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
            actorCarries,
        IReadOnlyList<string> custodyStackIds,
        int totalQuantity,
        long totalMassGrams)
    {
        BatchCommitId = batchCommitId ?? string.Empty;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        OriginPosition = originPosition;
        ActorCarries = Array.AsReadOnly((actorCarries
                ?? Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Select(value => value?.Clone())
            .OrderBy(
                value => ProductionCapacityRoutingDrainFingerprint
                    .ActorCarryKey(value),
                StringComparer.Ordinal)
            .ToArray());
        CustodyStackIds = Array.AsReadOnly((custodyStackIds
                ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
        TotalQuantity = totalQuantity;
        TotalMassGrams = totalMassGrams;
    }

    public string BatchCommitId { get; }
    public string SourceDestinationId { get; }
    public Vector2Int OriginPosition { get; }
    public IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
        ActorCarries { get; }
    public IReadOnlyList<string> CustodyStackIds { get; }
    public int TotalQuantity { get; }
    public long TotalMassGrams { get; }
}

public interface IProductionCapacityRoutingPhysicalSourceQuery
{
    bool TryCapture(
        string batchCommitId,
        string sourceDestinationId,
        out ProductionCapacityRoutingPhysicalSourceSnapshot snapshot,
        out string failureReason);
}

public sealed class ProductionCapacityRoutingPhysicalSourceQuery :
    IProductionCapacityRoutingPhysicalSourceQuery
{
    private readonly IWorldItemQueryService world;
    private readonly ICharacterCarryInventoryRegistry carries;
    private readonly IPhysicalItemMassQuery mass;

    public ProductionCapacityRoutingPhysicalSourceQuery(
        IWorldItemQueryService world,
        ICharacterCarryInventoryRegistry carries,
        IPhysicalItemMassQuery mass)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.carries = carries
            ?? throw new ArgumentNullException(nameof(carries));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
    }

    public bool TryCapture(
        string batchCommitId,
        string sourceDestinationId,
        out ProductionCapacityRoutingPhysicalSourceSnapshot snapshot,
        out string failureReason)
    {
        snapshot = null;
        failureReason = string.Empty;
        if (!IsCanonical(batchCommitId) || !IsCanonical(sourceDestinationId))
        {
            failureReason =
                "production-capacity-routing-physical-source-request-invalid";
            return false;
        }

        List<ProductionCapacityRoutingDrainActorCarrySaveData> actorRows = new();
        List<string> stackIds = new();
        List<Vector2Int> origins = new();
        Dictionary<string, WorldItemStackSnapshot> carriedWorldRows = new(
            StringComparer.Ordinal);
        long totalMass = 0L;
        int totalQuantity = 0;

        foreach (WorldItemStackSnapshot stack in world.GetAllStacks()
                     .Where(value => value != null)
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            if (!TryReadMatching(
                    stack.Components,
                    batchCommitId,
                    sourceDestinationId,
                    out FacilityOutputExactRouteCustodyMetadata custody))
            {
                continue;
            }
            long stackMass = GetMass(
                stack.ItemId,
                stack.ItemInstanceId,
                stack.Components,
                stack.Quantity);
            if (!IsCanonical(stack.StackId)
                || stack.Quantity <= 0
                || stack.Quantity != custody.Quantity
                || stackMass != custody.MassGrams
                || !string.Equals(
                    stack.ItemId,
                    custody.ItemId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-capacity-routing-world-custody-invalid";
                return false;
            }

            // Carried and in-transit records remain in the world repository,
            // while CharacterCarryInventory owns their actor/operation join.
            // Counting both projections duplicates the same physical stack.
            // Freeze the repository row here, then add its grams exactly once
            // from the carry authority after validating the 1:1 join below.
            if (stack.State is WorldItemStackState.Carried
                or WorldItemStackState.InTransit)
            {
                if (!carriedWorldRows.TryAdd(stack.StackId, stack))
                {
                    failureReason =
                        "production-capacity-routing-carried-world-row-duplicate";
                    return false;
                }
                continue;
            }

            stackIds.Add(stack.StackId);
            origins.Add(custody.OriginPosition);
            totalQuantity = checked(totalQuantity + stack.Quantity);
            totalMass = checked(totalMass + stackMass);
        }

        foreach (CharacterCarryInventory inventory in carries.All
                     .Where(value => value != null)
                     .OrderBy(value => value.CharacterId.Value,
                         StringComparer.Ordinal))
        {
            foreach (CharacterCarriedItemSaveData item in inventory.Items
                         .Where(value => value != null)
                         .OrderBy(value => value.carriedStackId,
                             StringComparer.Ordinal))
            {
                if (!TryReadMatching(
                        item.components,
                        batchCommitId,
                        sourceDestinationId,
                        out FacilityOutputExactRouteCustodyMetadata custody))
                {
                    continue;
                }
                if (!inventory.CharacterId.IsValid
                    || !IsCanonical(item.carriedStackId)
                    || !IsCanonical(item.sourceStackId)
                    || !IsCanonical(item.ownerOperationId)
                    || !IsCanonical(custody.RouteOperationId)
                    || item.quantity <= 0)
                {
                    failureReason =
                        "production-capacity-routing-carried-custody-invalid";
                    return false;
                }

                long itemMass = GetMass(
                    item.itemId,
                    item.itemInstanceId,
                    item.components,
                    item.quantity);
                if (item.quantity != custody.Quantity
                    || itemMass != custody.MassGrams
                    || !string.Equals(
                        item.itemId,
                        custody.ItemId,
                        StringComparison.Ordinal))
                {
                    failureReason =
                        "production-capacity-routing-carried-custody-mass-drift";
                    return false;
                }
                if (!carriedWorldRows.TryGetValue(
                        item.carriedStackId,
                        out WorldItemStackSnapshot carriedWorld)
                    || carriedWorld == null
                    || carriedWorld.Quantity != item.quantity
                    || !string.Equals(
                        carriedWorld.ItemId,
                        item.itemId,
                        StringComparison.Ordinal)
                    || GetMass(
                        carriedWorld.ItemId,
                        carriedWorld.ItemInstanceId,
                        carriedWorld.Components,
                        carriedWorld.Quantity) != itemMass
                    || !string.Equals(
                        ProductionCapacityRoutingDrainFingerprint
                            .CreateActorCarryStackSignature(
                                carriedWorld.ItemId,
                                carriedWorld.ItemInstanceId,
                                carriedWorld.Components),
                        ProductionCapacityRoutingDrainFingerprint
                            .CreateActorCarryStackSignature(
                                item.itemId,
                                item.itemInstanceId,
                                item.components),
                        StringComparison.Ordinal))
                {
                    failureReason =
                        "production-capacity-routing-carried-world-join-invalid:"
                        + item.carriedStackId;
                    return false;
                }
                actorRows.Add(new ProductionCapacityRoutingDrainActorCarrySaveData
                {
                    actorPersistentId = inventory.CharacterId.Value,
                    haulIntentOperationId = item.ownerOperationId,
                    routeOperationId = custody.RouteOperationId,
                    carriedStackId = item.carriedStackId,
                    sourceStackId = item.sourceStackId,
                    quantity = item.quantity,
                    massGrams = itemMass,
                    stackSignature = ProductionCapacityRoutingDrainFingerprint
                        .CreateActorCarryStackSignature(
                            item.itemId,
                            item.itemInstanceId,
                            item.components)
                });
                stackIds.Add(item.carriedStackId);
                origins.Add(custody.OriginPosition);
                totalQuantity = checked(totalQuantity + item.quantity);
                totalMass = checked(totalMass + itemMass);
            }
        }

        string[] joinedCarryStackIds = actorRows
            .Select(value => value.carriedStackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] carriedWorldStackIds = carriedWorldRows.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!joinedCarryStackIds.SequenceEqual(
                carriedWorldStackIds,
                StringComparer.Ordinal))
        {
            failureReason =
                "production-capacity-routing-carried-world-join-incomplete";
            return false;
        }

        string[] canonicalStackIds = stackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (canonicalStackIds.Length == 0
            || canonicalStackIds.Distinct(StringComparer.Ordinal).Count()
                != canonicalStackIds.Length
            || totalQuantity <= 0
            || totalMass <= 0L)
        {
            failureReason =
                "production-capacity-routing-physical-source-empty-or-duplicate";
            return false;
        }

        Vector2Int origin = origins[0];
        if (origins.Any(value => value != origin))
        {
            failureReason =
                "production-capacity-routing-origin-position-conflict";
            return false;
        }

        snapshot = new ProductionCapacityRoutingPhysicalSourceSnapshot(
            batchCommitId,
            sourceDestinationId,
            origin,
            actorRows,
            canonicalStackIds,
            totalQuantity,
            totalMass);
        return true;
    }

    private long GetMass(
        string itemId,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        int quantity)
    {
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            mass,
            (ItemDefinitionId)itemId,
            itemInstanceId,
            components);
        return mass.GetStackUnitMass((ItemDefinitionId)itemId, subject)
            .Multiply(quantity)
            .Value;
    }

    private static bool TryReadMatching(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        string batchCommitId,
        string sourceDestinationId,
        out FacilityOutputExactRouteCustodyMetadata custody) =>
        FacilityOutputExactRouteCustodyCodec.TryRead(components, out custody)
        && string.Equals(
            custody.BatchCommitId,
            batchCommitId,
            StringComparison.Ordinal)
        && string.Equals(
            custody.OriginDestinationId,
            sourceDestinationId,
            StringComparison.Ordinal);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
