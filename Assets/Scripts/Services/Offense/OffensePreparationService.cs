using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OffensePreparationSnapshot
{
    public OffensePreparationSnapshot(
        OffenseExpeditionPreparation preparation,
        IReadOnlyDictionary<OffenseSupplyType, int> availableSupplies)
    {
        Preparation = preparation ?? new OffenseExpeditionPreparation();
        AvailableSupplies = availableSupplies != null
            ? new Dictionary<OffenseSupplyType, int>(availableSupplies)
            : Enum.GetValues(typeof(OffenseSupplyType))
                .Cast<OffenseSupplyType>()
                .ToDictionary(type => type, _ => 0);
    }

    public OffenseExpeditionPreparation Preparation { get; }
    public IReadOnlyDictionary<OffenseSupplyType, int> AvailableSupplies { get; }

    public int GetAvailable(OffenseSupplyType type)
    {
        return AvailableSupplies.TryGetValue(type, out int amount) ? amount : 0;
    }
}

public interface IOffensePreparationService
{
    OffensePreparationSnapshot Evaluate();
    OffenseSupplyPackingSnapshot GetPackingSnapshot(string packageId);
    bool IsPackageReady(string packageId);
    bool TryCommitLoadout(
        OffenseSupplyLoadout loadout,
        OffenseExpeditionPreparation preparation,
        string packageId,
        out string message);
    bool TryConsumePackedSupplies(string packageId, out string message);
    void ConsumePackedSupplies(string packageId);
    void AbandonPackedSupplies(string packageId);
    void ReturnSupplies(OffenseSupplyLoadout loadout, string packageId = "");
    void DepositLoot(IReadOnlyDictionary<StockCategory, int> loot);
    IReadOnlyList<OffenseSupplyPackingStateData> CapturePackingState();
    void RestorePackingState(
        IEnumerable<OffenseSupplyPackingStateData> restored,
        DungeonGameRestoreReport report = null);
}

[Serializable]
public sealed class OffenseSupplyPackingItemStateData
{
    public string itemId = string.Empty;
    public int amount;
}

[Serializable]
public sealed class OffenseSupplyPackingStateData
{
    public string packageId = string.Empty;
    public string destinationId = string.Empty;
    public int stagingX;
    public int stagingY;
    public bool consumed;
    public List<OffenseSupplyPackingItemStateData> costs = new();

    public Vector2Int StagingPosition => new Vector2Int(stagingX, stagingY);
}

public readonly struct OffenseSupplyPackingSnapshot
{
    public OffenseSupplyPackingSnapshot(
        string packageId,
        int required,
        int delivered,
        bool consumed)
    {
        PackageId = packageId ?? string.Empty;
        Required = Mathf.Max(0, required);
        Delivered = Mathf.Clamp(delivered, 0, Required);
        Consumed = consumed;
    }

    public string PackageId { get; }
    public int Required { get; }
    public int Delivered { get; }
    public bool Consumed { get; }
    public bool Exists => !string.IsNullOrWhiteSpace(PackageId);
    public bool IsReady => Exists && !Consumed && Delivered >= Required;
    public bool IsInTransit => Exists && !Consumed && Delivered < Required;
}

public sealed class DungeonOffensePreparationService : IOffensePreparationService
{
    internal sealed class PackingRestoreCandidate
    {
        internal PackingRestoreCandidate(
            Dictionary<string, ExpeditionSupplyPackage> packages)
        {
            Packages = packages;
        }

        internal Dictionary<string, ExpeditionSupplyPackage> Packages { get; }
    }

    private readonly IFacilityEvolutionWarehouseInventoryQuery inventoryQuery;
    private readonly IProductionItemGateway itemGateway;
    private readonly IExteriorZoneQuery exteriorZones;
    private Dictionary<string, ExpeditionSupplyPackage> packages =
        new Dictionary<string, ExpeditionSupplyPackage>(StringComparer.Ordinal);

    public DungeonOffensePreparationService(
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery,
        IProductionItemGateway itemGateway,
        IExteriorZoneQuery exteriorZones)
    {
        this.inventoryQuery = inventoryQuery ?? throw new ArgumentNullException(nameof(inventoryQuery));
        this.itemGateway = itemGateway
            ?? throw new ArgumentNullException(nameof(itemGateway));
        this.exteriorZones = exteriorZones
            ?? throw new ArgumentNullException(nameof(exteriorZones));
    }

    public OffensePreparationSnapshot Evaluate()
    {
        return new OffensePreparationSnapshot(
            new OffenseExpeditionPreparation(),
            CaptureAvailableSupplies());
    }

    public OffenseSupplyPackingSnapshot GetPackingSnapshot(string packageId)
    {
        if (!packages.TryGetValue(
                NormalizePackageId(packageId),
                out ExpeditionSupplyPackage package))
        {
            return default;
        }

        int delivered = package.Costs.Sum(pair =>
            Mathf.Min(
                pair.Value,
                itemGateway.CountDelivered(pair.Key, package.DestinationId)));
        return new OffenseSupplyPackingSnapshot(
            package.PackageId,
            package.Required,
            delivered,
            package.Consumed);
    }

    public bool IsPackageReady(string packageId)
    {
        string normalized = NormalizePackageId(packageId);
        if (!packages.TryGetValue(normalized, out ExpeditionSupplyPackage package))
        {
            return true;
        }

        EnsurePackageReservation(package);
        return package.Consumed || GetPackingSnapshot(normalized).IsReady;
    }

    public bool TryCommitLoadout(
        OffenseSupplyLoadout loadout,
        OffenseExpeditionPreparation preparation,
        string packageId,
        out string message)
    {
        loadout ??= new OffenseSupplyLoadout();
        preparation ??= new OffenseExpeditionPreparation();
        if (loadout.TotalCount > preparation.SupplyCapacity)
        {
            message = $"보급 한도를 초과했습니다. ({loadout.TotalCount}/{preparation.SupplyCapacity})";
            return false;
        }

        string normalizedPackageId = NormalizePackageId(packageId);
        if (loadout.TotalCount <= 0)
        {
            message = "추가 보급 없이 집결합니다.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalizedPackageId))
        {
            message = "원정 보급 패키지 ID가 없습니다.";
            return false;
        }

        if (packages.ContainsKey(normalizedPackageId))
        {
            message = "이미 준비 중인 원정 보급 패키지입니다.";
            return false;
        }

        if (!TryResolveStagingPosition(out Vector2Int stagingPosition))
        {
            message = "출정 집결지를 찾을 수 없습니다.";
            return false;
        }

        Dictionary<string, int> costs = BuildItemCosts(loadout);
        string destinationId = GetDestinationId(normalizedPackageId);
        foreach (KeyValuePair<string, int> pair in costs)
        {
            if (!itemGateway.RequestDelivery(
                    pair.Key,
                    pair.Value,
                    stagingPosition,
                    destinationId,
                    out int requested,
                    out string failureReason)
                || requested < pair.Value)
            {
                itemGateway.ReleaseDestination(destinationId, stagingPosition);
                message = string.IsNullOrWhiteSpace(failureReason)
                    ? "원정 보급품의 물리 운반 요청을 만들 수 없습니다."
                    : $"원정 보급 요청 실패: {failureReason}";
                return false;
            }
        }

        packages.Add(
            normalizedPackageId,
            new ExpeditionSupplyPackage(
                normalizedPackageId,
                destinationId,
                stagingPosition,
                costs));
        itemGateway.PrioritizeDestination(destinationId);
        message = $"보급 운반 중: 0/{loadout.TotalCount}";
        return true;
    }

    public bool TryConsumePackedSupplies(string packageId, out string message)
    {
        string normalized = NormalizePackageId(packageId);
        if (string.IsNullOrWhiteSpace(normalized)
            || !packages.TryGetValue(normalized, out ExpeditionSupplyPackage package))
        {
            message = "추가 보급품이 없습니다.";
            return true;
        }

        if (package.Consumed)
        {
            message = "원정 보급품을 이미 적재했습니다.";
            return true;
        }

        OffenseSupplyPackingSnapshot snapshot = GetPackingSnapshot(normalized);
        if (!snapshot.IsReady)
        {
            message = $"보급 운반 중: {snapshot.Delivered}/{snapshot.Required}";
            return false;
        }

        if (!itemGateway.ConsumeDelivered(
                package.DestinationId,
                package.Costs,
                out string failureReason))
        {
            message = string.IsNullOrWhiteSpace(failureReason)
                ? "집결지 보급품을 적재하지 못했습니다."
                : $"보급 적재 실패: {failureReason}";
            return false;
        }

        package.Consumed = true;
        message = $"보급 적재 완료: {snapshot.Required}";
        return true;
    }

    public void ConsumePackedSupplies(string packageId)
    {
        TryConsumePackedSupplies(packageId, out _);
    }

    public void AbandonPackedSupplies(string packageId)
    {
        string normalized = NormalizePackageId(packageId);
        if (!packages.TryGetValue(normalized, out ExpeditionSupplyPackage package))
        {
            return;
        }

        packages.Remove(normalized);
        if (!package.Consumed)
        {
            itemGateway.RemoveDestination(package.DestinationId);
        }
    }

    public void ReturnSupplies(OffenseSupplyLoadout loadout, string packageId = "")
    {
        if (loadout == null) return;
        string normalized = NormalizePackageId(packageId);
        if (packages.TryGetValue(normalized, out ExpeditionSupplyPackage package))
        {
            packages.Remove(normalized);
            if (!package.Consumed)
            {
                itemGateway.ReleaseDestination(
                    package.DestinationId,
                    package.StagingPosition);
                return;
            }
        }

        foreach (KeyValuePair<OffenseSupplyType, int> pair in loadout.Amounts)
        {
            Deposit(
                OffenseSupplyCatalog.GetPhysicalItemId(pair.Key),
                pair.Value);
        }
    }

    public void DepositLoot(IReadOnlyDictionary<StockCategory, int> loot)
    {
        int total = loot?.Values.Sum(value => Mathf.Max(0, value)) ?? 0;
        if (total > 0)
        {
            Deposit(OffenseLootItemIds.UnappraisedLoot, total);
        }
    }

    public IReadOnlyList<OffenseSupplyPackingStateData> CapturePackingState()
    {
        return packages.Values
            .OrderBy(package => package.PackageId, StringComparer.Ordinal)
            .Select(package => new OffenseSupplyPackingStateData
            {
                packageId = package.PackageId,
                destinationId = package.DestinationId,
                stagingX = package.StagingPosition.x,
                stagingY = package.StagingPosition.y,
                consumed = package.Consumed,
                costs = package.Costs
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new OffenseSupplyPackingItemStateData
                    {
                        itemId = pair.Key,
                        amount = pair.Value
                    })
                    .ToList()
            })
            .ToList();
    }

    public void RestorePackingState(
        IEnumerable<OffenseSupplyPackingStateData> restored,
        DungeonGameRestoreReport report = null)
    {
        PublishPackingRestore(PreparePackingRestore(restored));
    }

    internal PackingRestoreCandidate PreparePackingRestore(
        IEnumerable<OffenseSupplyPackingStateData> restored)
    {
        Dictionary<string, ExpeditionSupplyPackage> candidate =
            new Dictionary<string, ExpeditionSupplyPackage>(StringComparer.Ordinal);
        foreach (OffenseSupplyPackingStateData source in
                 restored ?? throw new ArgumentNullException(nameof(restored)))
        {
            string packageId = NormalizePackageId(source?.packageId);
            if (source == null
                || string.IsNullOrWhiteSpace(packageId)
                || !string.Equals(packageId, source.packageId, StringComparison.Ordinal)
                || candidate.ContainsKey(packageId))
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate offense supply package '{packageId}'.");
            }

            if (source.costs == null
                || source.costs.Count == 0
                || source.costs.Any(item => item == null
                    || string.IsNullOrWhiteSpace(item.itemId)
                    || !string.Equals(item.itemId, item.itemId.Trim(),
                        StringComparison.Ordinal)
                    || item.amount <= 0)
                || source.costs.Select(item => item.itemId)
                    .Distinct(StringComparer.Ordinal).Count()
                    != source.costs.Count)
            {
                throw new InvalidOperationException(
                    $"Offense supply package '{packageId}' has invalid, duplicate, or empty item costs.");
            }
            Dictionary<string, int> costs = source.costs.ToDictionary(
                item => item.itemId,
                item => item.amount,
                StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(source.destinationId)
                || !string.Equals(source.destinationId,
                    source.destinationId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Offense supply package '{packageId}' has no canonical destination ID.");
            }
            string destinationId = source.destinationId;
            ExpeditionSupplyPackage package = new ExpeditionSupplyPackage(
                packageId,
                destinationId,
                source.StagingPosition,
                costs)
            {
                Consumed = source.consumed
            };
            candidate.Add(packageId, package);
        }

        // Reservation records are world projections. They are recreated lazily by
        // normal package queries after the detached candidate becomes live.
        return new PackingRestoreCandidate(candidate);
    }

    internal void PublishPackingRestore(PackingRestoreCandidate candidate)
    {
        packages = (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .Packages;
    }

    private bool TryResolveStagingPosition(out Vector2Int position)
    {
        if (exteriorZones.TryGetZone(
                ExteriorZoneType.ExpeditionStaging,
                out ExteriorZoneMarker staging)
            && staging != null)
        {
            position = staging.centerPos;
            return true;
        }

        if (exteriorZones.TryGetZone(
                ExteriorZoneType.Entrance,
                out ExteriorZoneMarker entrance)
            && entrance != null)
        {
            position = entrance.centerPos;
            return true;
        }

        position = default;
        return false;
    }

    private IReadOnlyDictionary<OffenseSupplyType, int> CaptureAvailableSupplies()
    {
        WarehouseInventory[] inventories = GetInventories();
        return Enum.GetValues(typeof(OffenseSupplyType))
            .Cast<OffenseSupplyType>()
            .ToDictionary(
                type => type,
                type => itemGateway.CountAvailableStock(
                    OffenseSupplyCatalog.GetPhysicalItemId(type),
                    string.Empty));
    }

    private void Deposit(string itemId, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
        {
            return;
        }

        if (TryResolveReturnDropPosition(out Vector2Int dropPosition)
            && itemGateway.SpawnOutput(itemId, remaining, dropPosition))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Failed to create physical return item '{itemId}' x{remaining}.");
    }

    private bool TryResolveReturnDropPosition(out Vector2Int position)
    {
        if (exteriorZones.TryGetZone(
                ExteriorZoneType.DropZone,
                out ExteriorZoneMarker dropZone)
            && dropZone != null)
        {
            position = dropZone.centerPos;
            return true;
        }

        if (exteriorZones.TryGetZone(
                ExteriorZoneType.Entrance,
                out ExteriorZoneMarker entrance)
            && entrance != null)
        {
            position = entrance.centerPos;
            return true;
        }

        return TryResolveStagingPosition(out position);
    }

    private static Dictionary<string, int> BuildItemCosts(
        OffenseSupplyLoadout loadout)
    {
        Dictionary<string, int> costs =
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<OffenseSupplyType, int> pair in loadout.Amounts)
        {
            int amount = Mathf.Max(0, pair.Value);
            if (amount <= 0)
            {
                continue;
            }

            string itemId = OffenseSupplyCatalog.GetPhysicalItemId(pair.Key);
            costs.TryGetValue(itemId, out int current);
            costs[itemId] = current + amount;
        }

        return costs;
    }

    private bool EnsurePackageReservation(ExpeditionSupplyPackage package)
    {
        if (package == null || package.Consumed)
        {
            return true;
        }

        bool complete = true;
        foreach (KeyValuePair<string, int> cost in package.Costs)
        {
            int pending = itemGateway.CountPending(
                cost.Key,
                package.DestinationId);
            int missing = Mathf.Max(0, cost.Value - pending);
            if (missing <= 0)
            {
                continue;
            }

            if (!itemGateway.RequestDelivery(
                    cost.Key,
                    missing,
                    package.StagingPosition,
                    package.DestinationId,
                    out int requested,
                    out _)
                || requested < missing)
            {
                complete = false;
            }
        }

        itemGateway.PrioritizeDestination(package.DestinationId);
        return complete;
    }

    private WarehouseInventory[] GetInventories()
    {
        return inventoryQuery.GetWarehouses()
            .Where(warehouse => warehouse?.Inventory != null)
            .Select(warehouse => warehouse.Inventory)
            .ToArray();
    }

    private static bool IsOperational(BuildableObject building)
    {
        return building != null
            && building.BuildingData != null
            && !building.isDestroy
            && !building.IsDamaged
            && building.gameObject.activeInHierarchy;
    }

    private static string NormalizePackageId(string packageId)
    {
        return packageId?.Trim() ?? string.Empty;
    }

    private static string GetDestinationId(string packageId)
    {
        return $"expedition:{NormalizePackageId(packageId)}";
    }

    internal sealed class ExpeditionSupplyPackage
    {
        public ExpeditionSupplyPackage(
            string packageId,
            string destinationId,
            Vector2Int stagingPosition,
            IReadOnlyDictionary<string, int> costs)
        {
            PackageId = packageId;
            DestinationId = destinationId;
            StagingPosition = stagingPosition;
            Costs = new Dictionary<string, int>(
                costs ?? new Dictionary<string, int>(),
                StringComparer.Ordinal);
            Required = Costs.Values.Sum();
        }

        public string PackageId { get; }
        public string DestinationId { get; }
        public Vector2Int StagingPosition { get; }
        public IReadOnlyDictionary<string, int> Costs { get; }
        public int Required { get; }
        public bool Consumed { get; set; }
    }
}
