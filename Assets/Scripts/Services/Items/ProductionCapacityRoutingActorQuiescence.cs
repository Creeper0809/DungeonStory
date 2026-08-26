using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class ProductionCapacityRoutingActorPlanSnapshot
{
    public ProductionCapacityRoutingActorPlanSnapshot(
        string actorPersistentId,
        IEnumerable<string> operationIds,
        IEnumerable<string> quantityLeaseIds,
        IEnumerable<string> pickedLeaseIds,
        IEnumerable<string> warehouseAdmissionTokenIds,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition)
    {
        ActorPersistentId = actorPersistentId ?? string.Empty;
        OperationIds = Canonical(operationIds);
        QuantityLeaseIds = Canonical(quantityLeaseIds);
        PickedLeaseIds = Canonical(pickedLeaseIds);
        WarehouseAdmissionTokenIds = Canonical(warehouseAdmissionTokenIds);
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        DeliveryPosition = deliveryPosition;
        DropPosition = dropPosition;
        Fingerprint = ProductionCapacityRoutingActorQuiescenceFingerprint
            .CreatePlan(this);
    }

    public string ActorPersistentId { get; }
    public IReadOnlyList<string> OperationIds { get; }
    public IReadOnlyList<string> QuantityLeaseIds { get; }
    public IReadOnlyList<string> PickedLeaseIds { get; }
    public IReadOnlyList<string> WarehouseAdmissionTokenIds { get; }
    public WorldItemHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public Vector2Int DeliveryPosition { get; }
    public Vector2Int DropPosition { get; }
    public string Fingerprint { get; }

    private static IReadOnlyList<string> Canonical(IEnumerable<string> values) =>
        Array.AsReadOnly((values ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
}

/// <summary>
/// Immutable, Items-owned command for moving the exact carried descendants of
/// one capacity-routing drain to the carrier's current cell.  This is not a
/// recovery drop: the exact-route destination, custody, lease and haul intent
/// remain authoritative until the caller durably records the receipt and
/// invokes the separate operation-authority release phase.
/// </summary>
public sealed class ProductionCapacityRoutingActorQuiescenceRequest
{
    private readonly IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
        expectedCarries;

    public ProductionCapacityRoutingActorQuiescenceRequest(
        string stepOperationId,
        string batchCommitId,
        string drainRequestFingerprint,
        string actorPersistentId,
        ProductionCapacityRoutingActorPlanSnapshot plan,
        IEnumerable<ProductionCapacityRoutingDrainActorCarrySaveData>
            expectedCarries)
    {
        StepOperationId = stepOperationId ?? string.Empty;
        BatchCommitId = batchCommitId ?? string.Empty;
        DrainRequestFingerprint = drainRequestFingerprint ?? string.Empty;
        ActorPersistentId = actorPersistentId ?? string.Empty;
        Plan = plan;
        this.expectedCarries = Array.AsReadOnly((expectedCarries
                ?? Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.haulIntentOperationId, StringComparer.Ordinal)
            .ToArray());
        RequestFingerprint =
            ProductionCapacityRoutingActorQuiescenceFingerprint.CreateRequest(
                StepOperationId,
                BatchCommitId,
                DrainRequestFingerprint,
                ActorPersistentId,
                Plan?.Fingerprint,
                this.expectedCarries);
    }

    public string StepOperationId { get; }
    public string BatchCommitId { get; }
    public string DrainRequestFingerprint { get; }
    public string ActorPersistentId { get; }
    public ProductionCapacityRoutingActorPlanSnapshot Plan { get; }
    public IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
        ExpectedCarries => expectedCarries;
    public string RequestFingerprint { get; }
}

public readonly struct ProductionCapacityRoutingActorQuiescenceResult
{
    public ProductionCapacityRoutingActorQuiescenceResult(
        ProductionCapacityRoutingDrainStatus status,
        Vector2Int physicalCell,
        int quantity,
        long massGrams,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        string failureReason)
    {
        Status = status;
        PhysicalCell = physicalCell;
        Quantity = quantity;
        MassGrams = massGrams;
        Receipt = receipt?.Clone();
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionCapacityRoutingDrainStatus Status { get; }
    public Vector2Int PhysicalCell { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public ProductionCapacityRoutingActorQuiesceReceiptSaveData Receipt { get; }
    public string ReceiptFingerprint => Receipt?.receiptFingerprint ?? string.Empty;
    public string FailureReason { get; }
    public bool IsSuccess => Status is ProductionCapacityRoutingDrainStatus.Applied
        or ProductionCapacityRoutingDrainStatus.Replay;
}

public interface IProductionCapacityRoutingActorQuiescence
{
    [GameplayInternalOnly(
        "Atomically relocates one frozen exact-route actor carry vector without releasing its operation authority.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingActorQuiescenceResult TryQuiesceAtCurrentCell(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        ProductionCapacityRoutingActorQuiescenceRequest request);

    bool TryVerifyDurableReceipt(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        out string failureReason);
}

internal static class ProductionCapacityRoutingActorQuiescenceFingerprint
{
    internal static string CreateRequest(
        string stepOperationId,
        string batchCommitId,
        string drainRequestFingerprint,
        string actorPersistentId,
        string planFingerprint,
        IEnumerable<ProductionCapacityRoutingDrainActorCarrySaveData> carries) =>
        Hash(CanonicalTokens(
            "capacity-routing-actor-quiescence-request-v1",
            stepOperationId,
            batchCommitId,
            drainRequestFingerprint,
            actorPersistentId,
            planFingerprint,
            default,
            carries));

    internal static string CreatePlan(
        ProductionCapacityRoutingActorPlanSnapshot plan)
    {
        IEnumerable<string> tokens = new[]
        {
            "capacity-routing-actor-plan-v1",
            plan?.ActorPersistentId ?? string.Empty,
            (plan == null ? -1 : (int)plan.DestinationKind).ToString(
                CultureInfo.InvariantCulture),
            plan?.DestinationId ?? string.Empty,
            (plan?.DeliveryPosition.x ?? 0).ToString(
                CultureInfo.InvariantCulture),
            (plan?.DeliveryPosition.y ?? 0).ToString(
                CultureInfo.InvariantCulture),
            (plan?.DropPosition.x ?? 0).ToString(CultureInfo.InvariantCulture),
            (plan?.DropPosition.y ?? 0).ToString(CultureInfo.InvariantCulture)
        }
        .Concat(plan?.OperationIds ?? Array.Empty<string>())
        .Concat(new[] { "#leases" })
        .Concat(plan?.QuantityLeaseIds ?? Array.Empty<string>())
        .Concat(new[] { "#picked" })
        .Concat(plan?.PickedLeaseIds ?? Array.Empty<string>())
        .Concat(new[] { "#admissions" })
        .Concat(plan?.WarehouseAdmissionTokenIds ?? Array.Empty<string>());
        return Hash(tokens);
    }

    private static IEnumerable<string> CanonicalTokens(
        string kind,
        string stepOperationId,
        string batchCommitId,
        string drainRequestFingerprint,
        string actorPersistentId,
        string planFingerprint,
        Vector2Int cell,
        IEnumerable<ProductionCapacityRoutingDrainActorCarrySaveData> carries)
    {
        yield return kind ?? string.Empty;
        yield return stepOperationId ?? string.Empty;
        yield return batchCommitId ?? string.Empty;
        yield return drainRequestFingerprint ?? string.Empty;
        yield return actorPersistentId ?? string.Empty;
        yield return planFingerprint ?? string.Empty;
        yield return cell.x.ToString(CultureInfo.InvariantCulture);
        yield return cell.y.ToString(CultureInfo.InvariantCulture);
        foreach (ProductionCapacityRoutingDrainActorCarrySaveData carry in
                 (carries
                    ?? Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>())
                 .Where(value => value != null)
                 .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                 .ThenBy(value => value.haulIntentOperationId,
                     StringComparer.Ordinal))
        {
            yield return carry.actorPersistentId ?? string.Empty;
            yield return carry.haulIntentOperationId ?? string.Empty;
            yield return carry.routeOperationId ?? string.Empty;
            yield return carry.carriedStackId ?? string.Empty;
            yield return carry.sourceStackId ?? string.Empty;
            yield return carry.quantity.ToString(CultureInfo.InvariantCulture);
            yield return carry.massGrams.ToString(CultureInfo.InvariantCulture);
            yield return carry.stackSignature ?? string.Empty;
        }
    }

    private static string Hash(IEnumerable<string> tokens)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\u001f", tokens));
        byte[] digest = sha.ComputeHash(bytes);
        StringBuilder result = new(digest.Length * 2);
        foreach (byte value in digest)
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }
}
