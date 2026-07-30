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
    private readonly IFacilityEvolutionWarehouseInventoryQuery inventoryQuery;
    private readonly IProductionItemGateway itemGateway;
    private readonly IExteriorZoneQuery exteriorZones;
    private readonly Dictionary<string, ExpeditionSupplyPackage> packages =
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
            Deposit(OffenseSupplyCatalog.GetStockCategory(pair.Key), pair.Value);
        }
    }

    public void DepositLoot(IReadOnlyDictionary<StockCategory, int> loot)
    {
        if (loot == null) return;
        foreach (KeyValuePair<StockCategory, int> pair in loot)
        {
            Deposit(pair.Key, pair.Value);
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
        packages.Clear();
        foreach (OffenseSupplyPackingStateData source in
                 restored ?? Array.Empty<OffenseSupplyPackingStateData>())
        {
            string packageId = NormalizePackageId(source?.packageId);
            if (source == null
                || string.IsNullOrWhiteSpace(packageId)
                || packages.ContainsKey(packageId))
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate offense supply package '{packageId}'.");
            }

            Dictionary<string, int> costs = (source.costs
                    ?? new List<OffenseSupplyPackingItemStateData>())
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.itemId)
                    && item.amount > 0)
                .GroupBy(item => item.itemId.Trim(), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => item.amount),
                    StringComparer.Ordinal);
            if (costs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Offense supply package '{packageId}' has no item costs.");
            }

            string destinationId = string.IsNullOrWhiteSpace(source.destinationId)
                ? GetDestinationId(packageId)
                : source.destinationId.Trim();
            ExpeditionSupplyPackage package = new ExpeditionSupplyPackage(
                packageId,
                destinationId,
                source.StagingPosition,
                costs)
            {
                Consumed = source.consumed
            };
            packages.Add(packageId, package);
            if (!package.Consumed && !EnsurePackageReservation(package))
            {
                report?.AddWarning(
                    $"원정 '{packageId}' 보급 예약 일부를 즉시 복구하지 못했습니다. "
                    + "재고가 들어오면 다시 예약합니다.");
            }
        }
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
                type => inventories.Sum(inventory => inventory.GetStock(
                    OffenseSupplyCatalog.GetStockCategory(type))));
    }

    private void Deposit(StockCategory category, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
        {
            return;
        }

        string itemId = DungeonItemCatalogSO.StockItemId(category);
        if (TryResolveReturnDropPosition(out Vector2Int dropPosition)
            && itemGateway.SpawnOutput(itemId, remaining, dropPosition))
        {
            return;
        }

        WarehouseInventory inventory = GetInventories()
            .Where(value => value.Accepts(category))
            .OrderByDescending(value => value.RemainingCapacity)
            .FirstOrDefault();
        if (inventory != null)
        {
            inventory.Deposit(category, remaining);
        }
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

            string itemId = DungeonItemCatalogSO.StockItemId(
                OffenseSupplyCatalog.GetStockCategory(pair.Key));
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
        return inventoryQuery.GetInventories()
            .Where(inventory => inventory != null)
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

    private sealed class ExpeditionSupplyPackage
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
