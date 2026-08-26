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

public readonly struct OffenseSupplyCustodyReceipt
{
    public OffenseSupplyCustodyReceipt(
        string operationId,
        string reasonCode,
        string commitId,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long massGrams)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        CommitId = commitId ?? string.Empty;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Quantity = quantity;
        MassGrams = massGrams;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string CommitId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public bool IsCommitted => IsCanonical(OperationId)
        && IsCanonical(ReasonCode)
        && IsCanonical(CommitId)
        && SourceStackIds?.Count > 0
        && SourceStackIds.All(IsCanonical)
        && SourceStackIds.Distinct(StringComparer.Ordinal).Count()
            == SourceStackIds.Count
        && SourceStackIds.SequenceEqual(
            SourceStackIds.OrderBy(value => value, StringComparer.Ordinal),
            StringComparer.Ordinal)
        && Quantity > 0
        && MassGrams > 0L;

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IOffenseSupplyPhysicalCustodyGateway
{
    bool TryCommitTransferPending(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        string reasonCode,
        out OffenseSupplyCustodyReceipt receipt,
        out string failureReason);

    bool TryGetPending(
        string operationId,
        out OffenseSupplyCustodyReceipt receipt);

    bool AcknowledgeTransfer(string commitId, out string failureReason);

    bool TryEnsureReturnOutputs(
        IReadOnlyDictionary<string, int> outputs,
        Vector2Int outputPosition,
        string operationId,
        string reasonCode,
        out PhysicalItemSourcePublicationReceipt receipt,
        out string failureReason);
}

public sealed class OffenseSupplyPhysicalCustodyGateway :
    IOffenseSupplyPhysicalCustodyGateway
{
    private readonly IStockQuery stock;
    private readonly IPhysicalItemBatchDispositionService dispositions;
    private readonly IPhysicalItemSourcePublicationService sources;

    public OffenseSupplyPhysicalCustodyGateway(
        IStockQuery stock,
        IPhysicalItemBatchDispositionService dispositions,
        IPhysicalItemSourcePublicationService sources)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));
        this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public bool TryCommitTransferPending(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        string reasonCode,
        out OffenseSupplyCustodyReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (TryGetPending(operationId, out OffenseSupplyCustodyReceipt existing))
        {
            int expected = (costs ?? new Dictionary<string, int>())
                .Where(pair => pair.Value > 0)
                .Sum(pair => pair.Value);
            if (existing.Quantity != expected
                || !string.Equals(
                    existing.ReasonCode,
                    reasonCode,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "offense-supply-custody-operation-conflict:"
                    + operationId;
                return false;
            }
            receipt = existing;
            failureReason = string.Empty;
            return true;
        }

        List<PhysicalItemTransformInput> inputs = new();
        foreach (KeyValuePair<string, int> cost in (costs
                     ?? new Dictionary<string, int>())
                 .Where(pair => pair.Value > 0)
                 .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            int remaining = cost.Value;
            foreach (WorldItemStackSnapshot stack in stock.GetAllStacks()
                         .Where(value => value != null
                             && value.State
                                 == WorldItemStackState.FacilityBuffer
                             && value.ReservedQuantity == 0
                             && string.IsNullOrEmpty(
                                 value.ReservedByPersistentId)
                             && string.Equals(
                                 value.DestinationId,
                                 destinationId,
                                 StringComparison.Ordinal)
                             && string.Equals(
                                 value.ItemId,
                                 cost.Key,
                                 StringComparison.Ordinal))
                         .OrderBy(
                             value => value.StackId,
                             StringComparer.Ordinal))
            {
                if (remaining <= 0)
                {
                    break;
                }
                int take = Math.Min(remaining, stack.AvailableQuantity);
                if (take <= 0)
                {
                    continue;
                }
                inputs.Add(new PhysicalItemTransformInput(stack.StackId, take));
                remaining -= take;
            }
            if (remaining > 0)
            {
                failureReason = "offense-supply-custody-item-missing:"
                    + cost.Key;
                return false;
            }
        }
        if (inputs.Count == 0)
        {
            failureReason = "offense-supply-custody-empty-request";
            return false;
        }
        if (!dispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                reasonCode,
                out PhysicalItemBatchDispositionReceipt physical,
                out failureReason))
        {
            return false;
        }
        receipt = FromPhysical(physical);
        if (!receipt.IsCommitted)
        {
            receipt = default;
            failureReason = "offense-supply-custody-receipt-invalid";
            return false;
        }
        return true;
    }

    public bool TryGetPending(
        string operationId,
        out OffenseSupplyCustodyReceipt receipt)
    {
        if (dispositions.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt physical)
            && physical.Kind == PhysicalItemDispositionKind.Transfer)
        {
            receipt = FromPhysical(physical);
            return receipt.IsCommitted;
        }
        receipt = default;
        return false;
    }

    public bool AcknowledgeTransfer(
        string commitId,
        out string failureReason) =>
        dispositions.Acknowledge(commitId, out failureReason);

    public bool TryEnsureReturnOutputs(
        IReadOnlyDictionary<string, int> outputs,
        Vector2Int outputPosition,
        string operationId,
        string reasonCode,
        out PhysicalItemSourcePublicationReceipt receipt,
        out string failureReason) => sources.TryEnsureLooseOutputs(
        outputs,
        outputPosition,
        operationId,
        reasonCode,
        out receipt,
        out failureReason);

    private static OffenseSupplyCustodyReceipt FromPhysical(
        PhysicalItemBatchDispositionReceipt receipt) => new(
        receipt.OperationId,
        receipt.ReasonCode,
        receipt.CommitId,
        receipt.SourceStackIds,
        receipt.Quantity,
        receipt.InputMassGrams);
}

public enum OffenseSupplyCustodyPhase
{
    Staging = 0,
    CustodyOwned = 1,
    ReturnPublishing = 2,
    Returned = 3,
    Lost = 4
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
    public int custodyPhase;
    public string custodyOperationId = string.Empty;
    public string custodyReasonCode = string.Empty;
    public string custodyCommitId = string.Empty;
    public List<string> custodySourceStackIds = new();
    public int custodyQuantity;
    public long custodyMassGrams;
    public bool custodyAcknowledged;
    public string returnOperationId = string.Empty;
    public string returnReasonCode = string.Empty;
    public int returnX;
    public int returnY;
    public List<string> returnOutputCommitIds = new();
    public int returnQuantity;
    public long returnMassGrams;
    public long consumedOrLostMassGrams;
    public List<OffenseSupplyPackingItemStateData> returnedCosts = new();
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

public sealed class DungeonOffensePreparationService :
    IOffensePreparationService,
    IDungeonRestoreTransactionParticipant
{
    private const string ExpeditionSupplyOwnerDomain =
        "offense.expedition-supply";
    private const string RestoreParticipantId =
        "219.world.offense-supply-packages";
    public const string CustodyTransferReasonCode =
        "offense-expedition-supply-custody-transfer";
    public const string ReturnSourceReasonCode =
        "offense-expedition-supply-return";
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
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly IFacilityBufferDestinationClaimCommand destinationClaimCommands;
    private readonly IOffenseSupplyPhysicalCustodyGateway physicalCustody;
    private Dictionary<string, ExpeditionSupplyPackage> packages =
        new Dictionary<string, ExpeditionSupplyPackage>(StringComparer.Ordinal);
    private PackingRestoreCandidate stagedRestore;
    private Dictionary<string, ExpeditionSupplyPackage> previousPackages;
    private bool restoreActive;
    private bool restorePublished;

    public string ParticipantId => RestoreParticipantId;

    public DungeonOffensePreparationService(
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery,
        IProductionItemGateway itemGateway,
        IExteriorZoneQuery exteriorZones,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        IFacilityBufferDestinationClaimCommand destinationClaimCommands,
        IOffenseSupplyPhysicalCustodyGateway physicalCustody)
    {
        this.inventoryQuery = inventoryQuery ?? throw new ArgumentNullException(nameof(inventoryQuery));
        this.itemGateway = itemGateway
            ?? throw new ArgumentNullException(nameof(itemGateway));
        this.exteriorZones = exteriorZones
            ?? throw new ArgumentNullException(nameof(exteriorZones));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.destinationClaimCommands = destinationClaimCommands
            ?? throw new ArgumentNullException(nameof(destinationClaimCommands));
        this.physicalCustody = physicalCustody
            ?? throw new ArgumentNullException(nameof(physicalCustody));
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
                out ExpeditionSupplyPackage package)
            || package.IsTerminal)
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
        if (package.Phase == OffenseSupplyCustodyPhase.CustodyOwned)
        {
            return true;
        }
        if (physicalCustody.TryGetPending(
                FormatCustodyOperationId(package.PackageId),
                out _))
        {
            return true;
        }
        return GetPackingSnapshot(normalized).IsReady;
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
        ExpeditionSupplyPackage package = new ExpeditionSupplyPackage(
            normalizedPackageId,
            destinationId,
            stagingPosition,
            costs);
        FacilityBufferDestinationClaim claim = CreateDestinationClaim(package);
        if (!destinationClaimCommands.TryClaim(
                claim,
                out FacilityBufferDestinationClaimFailureCode claimFailure,
                out string claimReason))
        {
            message = string.IsNullOrWhiteSpace(claimReason)
                ? $"원정 집결지 소유권을 만들 수 없습니다. ({claimFailure})"
                : $"원정 집결지 소유권 실패: {claimReason}";
            return false;
        }

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
                RevokeDestinationClaimOrThrow(claim);
                message = string.IsNullOrWhiteSpace(failureReason)
                    ? "원정 보급품의 물리 운반 요청을 만들 수 없습니다."
                    : $"원정 보급 요청 실패: {failureReason}";
                return false;
            }
        }

        packages.Add(normalizedPackageId, package);
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

        if (package.IsTerminal)
        {
            message = "원정 보급품 소유권이 이미 종료되었습니다.";
            return true;
        }

        if (package.Phase == OffenseSupplyCustodyPhase.CustodyOwned)
        {
            if (!EnsureCustodyAcknowledged(package, out message))
            {
                return false;
            }
            RevokeDestinationClaimIfPresent(package);
            message = $"보급 적재 완료: {package.Required}";
            return true;
        }

        if (!HasExactDestinationClaim(package))
        {
            message = "원정 집결지 소유권이 유실되었습니다.";
            return false;
        }

        string operationId = FormatCustodyOperationId(package.PackageId);
        bool replayPending = physicalCustody.TryGetPending(
            operationId,
            out _);
        OffenseSupplyPackingSnapshot snapshot = GetPackingSnapshot(normalized);
        if (!replayPending && !snapshot.IsReady)
        {
            message = $"보급 운반 중: {snapshot.Delivered}/{snapshot.Required}";
            return false;
        }

        if (!physicalCustody.TryCommitTransferPending(
                package.DestinationId,
                package.Costs,
                operationId,
                CustodyTransferReasonCode,
                out OffenseSupplyCustodyReceipt receipt,
                out string failureReason))
        {
            message = string.IsNullOrWhiteSpace(failureReason)
                ? "집결지 보급품을 적재하지 못했습니다."
                : $"보급 적재 실패: {failureReason}";
            return false;
        }

        package.RecordCustody(receipt);
        if (!EnsureCustodyAcknowledged(package, out message))
        {
            return false;
        }
        RevokeDestinationClaimIfPresent(package);
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

        if (package.Phase == OffenseSupplyCustodyPhase.Staging)
        {
            itemGateway.ReleaseDestination(
                package.DestinationId,
                package.StagingPosition);
            RevokeDestinationClaimOrThrow(CreateDestinationClaim(package));
            packages.Remove(normalized);
            return;
        }
        if (package.Phase == OffenseSupplyCustodyPhase.ReturnPublishing)
        {
            throw new InvalidOperationException(
                $"Expedition supply package '{package.PackageId}' cannot be lost while return publication is pending.");
        }
        if (!package.IsTerminal)
        {
            if (!EnsureCustodyAcknowledged(package, out string failureReason))
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{package.PackageId}' loss acknowledgement failed: {failureReason}");
            }
            package.MarkLost();
        }
    }

    public void ReturnSupplies(OffenseSupplyLoadout loadout, string packageId = "")
    {
        if (loadout == null) return;
        string normalized = NormalizePackageId(packageId);
        if (string.IsNullOrWhiteSpace(normalized)
            || !packages.TryGetValue(
                normalized,
                out ExpeditionSupplyPackage package))
        {
            // Only the persisted package is authorized to materialize returns.
            // A caller-provided loadout without that owner must never mint stock.
            return;
        }
        if (package.Phase == OffenseSupplyCustodyPhase.Staging)
        {
            itemGateway.ReleaseDestination(
                package.DestinationId,
                package.StagingPosition);
            RevokeDestinationClaimIfPresent(package);
            packages.Remove(normalized);
            return;
        }
        if (package.Phase is OffenseSupplyCustodyPhase.Returned
                or OffenseSupplyCustodyPhase.Lost)
        {
            return;
        }
        if (!EnsureCustodyAcknowledged(package, out string custodyFailure))
        {
            throw new InvalidOperationException(
                $"Expedition supply package '{package.PackageId}' return acknowledgement failed: {custodyFailure}");
        }

        Dictionary<string, int> returned = BuildItemCosts(loadout);
        foreach (KeyValuePair<string, int> pair in returned)
        {
            if (!package.Costs.TryGetValue(pair.Key, out int owned)
                || pair.Value > owned)
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{package.PackageId}' attempted to return unowned item '{pair.Key}' x{pair.Value}.");
            }
        }

        if (package.Phase == OffenseSupplyCustodyPhase.ReturnPublishing)
        {
            if (!DictionaryEqual(package.ReturnedCosts, returned))
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{package.PackageId}' return retry changed its physical outputs.");
            }
        }
        else
        {
            if (!TryResolveReturnDropPosition(out Vector2Int dropPosition))
            {
                throw new InvalidOperationException(
                    "Failed to resolve the physical expedition supply return position.");
            }
            package.BeginReturn(returned, dropPosition);
        }

        if (package.ReturnedCosts.Count == 0)
        {
            package.CompleteEmptyReturn();
            return;
        }
        if (!physicalCustody.TryEnsureReturnOutputs(
                package.ReturnedCosts,
                package.ReturnPosition,
                package.ReturnOperationId,
                ReturnSourceReasonCode,
                out PhysicalItemSourcePublicationReceipt receipt,
                out string returnFailure))
        {
            throw new InvalidOperationException(
                $"Expedition supply package '{package.PackageId}' return publication failed: {returnFailure}");
        }
        package.CompleteReturn(receipt);
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
                custodyPhase = (int)package.Phase,
                custodyOperationId = package.CustodyOperationId,
                custodyReasonCode = package.CustodyReasonCode,
                custodyCommitId = package.CustodyCommitId,
                custodySourceStackIds = package.CustodySourceStackIds.ToList(),
                custodyQuantity = package.CustodyQuantity,
                custodyMassGrams = package.CustodyMassGrams,
                custodyAcknowledged = package.CustodyAcknowledged,
                returnOperationId = package.ReturnOperationId,
                returnReasonCode = package.ReturnReasonCode,
                returnX = package.ReturnPosition.x,
                returnY = package.ReturnPosition.y,
                returnOutputCommitIds = package.ReturnOutputCommitIds.ToList(),
                returnQuantity = package.ReturnQuantity,
                returnMassGrams = package.ReturnMassGrams,
                consumedOrLostMassGrams = package.ConsumedOrLostMassGrams,
                returnedCosts = package.ReturnedCosts
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new OffenseSupplyPackingItemStateData
                    {
                        itemId = pair.Key,
                        amount = pair.Value
                    })
                    .ToList(),
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
                    StringComparison.Ordinal)
                || !string.Equals(
                    source.destinationId,
                    GetDestinationId(packageId),
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
                costs);
            package.RestoreCustody(source);
            candidate.Add(packageId, package);
        }

        // Reservation records are world projections. They are recreated lazily by
        // normal package queries after the detached candidate becomes live.
        return new PackingRestoreCandidate(candidate);
    }

    internal void PublishPackingRestore(PackingRestoreCandidate candidate)
    {
        Dictionary<string, ExpeditionSupplyPackage> restored =
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .Packages;
        if (restoreActive && (restorePublished || stagedRestore != null))
        {
            throw new InvalidOperationException(
                "Offense supply package restore was staged more than once.");
        }
        ExpeditionSupplyPackage[] unconsumed = restored.Values
            .Where(package => !package.Consumed)
            .ToArray();
        if (unconsumed.Length > 0
            && (!TryResolveStagingPosition(out Vector2Int currentStaging)
                || unconsumed.Any(package =>
                    package.StagingPosition != currentStaging)))
        {
            throw new InvalidOperationException(
                "Offense supply package staging authority does not match the current world.");
        }

        FacilityBufferDestinationClaim[] desiredClaims = unconsumed
            .OrderBy(package => package.PackageId, StringComparer.Ordinal)
            .Select(CreateDestinationClaim)
            .ToArray();
        if (!destinationClaimCommands.TryReplaceOwnedClaims(
                ExpeditionSupplyOwnerDomain,
                desiredClaims,
                out FacilityBufferDestinationClaimFailureCode failureCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Offense supply destination restore failed: "
                + $"{failureCode}: {failureReason}");
        }
        if (restoreActive)
        {
            stagedRestore = candidate;
            return;
        }

        packages = restored;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
        {
            throw new InvalidOperationException(
                "Offense supply package restore is already active.");
        }

        previousPackages = packages;
        stagedRestore = null;
        restoreActive = true;
        restorePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreActive || restorePublished || stagedRestore == null)
        {
            throw new InvalidOperationException(
                "Offense supply package restore is not ready to publish.");
        }

        packages = stagedRestore.Packages;
        restorePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreActive)
            return;

        if (restorePublished && previousPackages != null)
            packages = previousPackages;
        ResetRestoreTransaction();
    }

    public void CompleteRestoreCandidate()
    {
        ResetRestoreTransaction();
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublished)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ResetRestoreTransaction();
    }

    private void ResetRestoreTransaction()
    {
        stagedRestore = null;
        previousPackages = null;
        restoreActive = false;
        restorePublished = false;
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

        if (!HasExactDestinationClaim(package))
        {
            throw new InvalidOperationException(
                $"Expedition supply package '{package.PackageId}' lost its exact staging claim.");
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

    private static FacilityBufferDestinationClaim CreateDestinationClaim(
        ExpeditionSupplyPackage package) =>
        new FacilityBufferDestinationClaim(
            package?.DestinationId ?? string.Empty,
            package?.StagingPosition ?? default,
            ExpeditionSupplyOwnerDomain,
            package?.PackageId ?? string.Empty,
            ownerFacilityId: null,
            FacilityBufferDestinationAnchorKind.ReservedTarget);

    private bool HasExactDestinationClaim(ExpeditionSupplyPackage package)
    {
        if (package == null
            || !destinationClaims.TryGetClaim(
                package.DestinationId,
                package.StagingPosition,
                out FacilityBufferDestinationClaim claim))
        {
            return false;
        }
        FacilityBufferDestinationClaim expected = CreateDestinationClaim(package);
        return string.Equals(
                claim.OwnerDomain,
                expected.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerOperationId,
                expected.OwnerOperationId,
                StringComparison.Ordinal)
            && claim.OwnerFacilityId == null
            && claim.AnchorKind
                == FacilityBufferDestinationAnchorKind.ReservedTarget;
    }

    private void RevokeDestinationClaimOrThrow(
        FacilityBufferDestinationClaim claim)
    {
        if (!destinationClaimCommands.TryRevoke(
                claim,
                out FacilityBufferDestinationClaimFailureCode failureCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                $"Offense supply destination revoke failed: {failureCode}: {failureReason}");
        }
    }

    private void RevokeDestinationClaimIfPresent(
        ExpeditionSupplyPackage package)
    {
        if (!destinationClaims.TryGetClaim(
                package.DestinationId,
                package.StagingPosition,
                out FacilityBufferDestinationClaim claim))
        {
            return;
        }
        if (!HasExactDestinationClaim(package))
        {
            throw new InvalidOperationException(
                $"Expedition supply package '{package.PackageId}' return/custody found a foreign staging claim.");
        }
        RevokeDestinationClaimOrThrow(claim);
    }

    private bool EnsureCustodyAcknowledged(
        ExpeditionSupplyPackage package,
        out string message)
    {
        if (package.CustodyAcknowledged)
        {
            message = string.Empty;
            return true;
        }
        if (!physicalCustody.AcknowledgeTransfer(
                package.CustodyCommitId,
                out string failureReason))
        {
            message = string.IsNullOrWhiteSpace(failureReason)
                ? "원정 보급품 소유권 영수증을 확인하지 못했습니다."
                : "원정 보급품 영수증 확인 실패: " + failureReason;
            return false;
        }
        package.MarkCustodyAcknowledged();
        message = string.Empty;
        return true;
    }

    public static string FormatCustodyOperationId(string packageId) =>
        "offense-supply-custody:" + NormalizePackageId(packageId);

    public static string FormatReturnOperationId(string packageId) =>
        "offense-supply-return:" + NormalizePackageId(packageId);

    private static bool DictionaryEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right) =>
        (left?.Count ?? 0) == (right?.Count ?? 0)
        && (left ?? new Dictionary<string, int>()).All(pair =>
            right != null
            && right.TryGetValue(pair.Key, out int value)
            && value == pair.Value);

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
        public OffenseSupplyCustodyPhase Phase { get; private set; }
        public bool Consumed => Phase != OffenseSupplyCustodyPhase.Staging;
        public bool IsTerminal => Phase is OffenseSupplyCustodyPhase.Returned
            or OffenseSupplyCustodyPhase.Lost;
        public string CustodyOperationId { get; private set; } = string.Empty;
        public string CustodyReasonCode { get; private set; } = string.Empty;
        public string CustodyCommitId { get; private set; } = string.Empty;
        public IReadOnlyList<string> CustodySourceStackIds =>
            custodySourceStackIds;
        public int CustodyQuantity { get; private set; }
        public long CustodyMassGrams { get; private set; }
        public bool CustodyAcknowledged { get; private set; }
        public string ReturnOperationId { get; private set; } = string.Empty;
        public string ReturnReasonCode { get; private set; } = string.Empty;
        public Vector2Int ReturnPosition { get; private set; }
        public IReadOnlyDictionary<string, int> ReturnedCosts => returnedCosts;
        public IReadOnlyList<string> ReturnOutputCommitIds =>
            returnOutputCommitIds;
        public int ReturnQuantity { get; private set; }
        public long ReturnMassGrams { get; private set; }
        public long ConsumedOrLostMassGrams { get; private set; }

        private List<string> custodySourceStackIds = new();
        private Dictionary<string, int> returnedCosts =
            new(StringComparer.Ordinal);
        private List<string> returnOutputCommitIds = new();

        public void RecordCustody(OffenseSupplyCustodyReceipt receipt)
        {
            if (Phase != OffenseSupplyCustodyPhase.Staging
                || !receipt.IsCommitted
                || receipt.Quantity != Required
                || !string.Equals(
                    receipt.OperationId,
                    FormatCustodyOperationId(PackageId),
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.ReasonCode,
                    CustodyTransferReasonCode,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' received a conflicting custody receipt.");
            }
            Phase = OffenseSupplyCustodyPhase.CustodyOwned;
            CustodyOperationId = receipt.OperationId;
            CustodyReasonCode = receipt.ReasonCode;
            CustodyCommitId = receipt.CommitId;
            custodySourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            CustodyQuantity = receipt.Quantity;
            CustodyMassGrams = receipt.MassGrams;
            CustodyAcknowledged = false;
        }

        public void MarkCustodyAcknowledged()
        {
            if (Phase != OffenseSupplyCustodyPhase.CustodyOwned
                || string.IsNullOrWhiteSpace(CustodyCommitId))
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' has no custody receipt to acknowledge.");
            }
            CustodyAcknowledged = true;
        }

        public void BeginReturn(
            IReadOnlyDictionary<string, int> outputs,
            Vector2Int position)
        {
            if (Phase != OffenseSupplyCustodyPhase.CustodyOwned)
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' cannot begin a return from phase '{Phase}'.");
            }
            if (!CustodyAcknowledged)
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' cannot return unacknowledged custody.");
            }
            returnedCosts = (outputs ?? new Dictionary<string, int>())
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);
            Phase = OffenseSupplyCustodyPhase.ReturnPublishing;
            ReturnOperationId = FormatReturnOperationId(PackageId);
            ReturnReasonCode = ReturnSourceReasonCode;
            ReturnPosition = position;
        }

        public void CompleteReturn(PhysicalItemSourcePublicationReceipt receipt)
        {
            if (Phase != OffenseSupplyCustodyPhase.ReturnPublishing
                || !CustodyAcknowledged
                || !receipt.IsCommitted
                || !string.Equals(
                    receipt.OperationId,
                    ReturnOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.ReasonCode,
                    ReturnReasonCode,
                    StringComparison.Ordinal)
                || receipt.OutputQuantity > CustodyQuantity
                || receipt.OutputMassGrams > CustodyMassGrams)
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' return receipt is invalid.");
            }
            returnOutputCommitIds = receipt.OutputCommitIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            ReturnQuantity = receipt.OutputQuantity;
            ReturnMassGrams = receipt.OutputMassGrams;
            ConsumedOrLostMassGrams = checked(
                CustodyMassGrams - ReturnMassGrams);
            Phase = OffenseSupplyCustodyPhase.Returned;
        }

        public void CompleteEmptyReturn()
        {
            if (Phase != OffenseSupplyCustodyPhase.ReturnPublishing
                || returnedCosts.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' empty return is invalid.");
            }
            ReturnQuantity = 0;
            ReturnMassGrams = 0L;
            ConsumedOrLostMassGrams = CustodyMassGrams;
            Phase = OffenseSupplyCustodyPhase.Returned;
        }

        public void MarkLost()
        {
            if (Phase != OffenseSupplyCustodyPhase.CustodyOwned)
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' cannot be lost from phase '{Phase}'.");
            }
            if (!CustodyAcknowledged)
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' cannot lose unacknowledged custody.");
            }
            returnedCosts.Clear();
            returnOutputCommitIds.Clear();
            ReturnQuantity = 0;
            ReturnMassGrams = 0L;
            ConsumedOrLostMassGrams = CustodyMassGrams;
            Phase = OffenseSupplyCustodyPhase.Lost;
        }

        public void RestoreCustody(OffenseSupplyPackingStateData source)
        {
            Phase = (OffenseSupplyCustodyPhase)source.custodyPhase;
            if (!Enum.IsDefined(typeof(OffenseSupplyCustodyPhase), Phase)
                || source.consumed != (Phase != OffenseSupplyCustodyPhase.Staging))
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' has invalid custody phase.");
            }
            if (Phase == OffenseSupplyCustodyPhase.Staging)
            {
                if (!HasEmptyCustody(source))
                {
                    throw new InvalidOperationException(
                        $"Staging expedition supply package '{PackageId}' contains custody provenance.");
                }
                return;
            }

            CustodyOperationId = source.custodyOperationId ?? string.Empty;
            CustodyReasonCode = source.custodyReasonCode ?? string.Empty;
            CustodyCommitId = source.custodyCommitId ?? string.Empty;
            custodySourceStackIds = (source.custodySourceStackIds
                    ?? new List<string>())
                .ToList();
            CustodyQuantity = source.custodyQuantity;
            CustodyMassGrams = source.custodyMassGrams;
            CustodyAcknowledged = source.custodyAcknowledged;
            if (!string.Equals(
                    CustodyOperationId,
                    FormatCustodyOperationId(PackageId),
                    StringComparison.Ordinal)
                || !string.Equals(
                    CustodyReasonCode,
                    CustodyTransferReasonCode,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(CustodyCommitId)
                || !string.Equals(
                    CustodyCommitId,
                    $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Transfer}:{CustodyOperationId}:{CustodyQuantity}:{CustodyMassGrams}",
                    StringComparison.Ordinal)
                || CustodyQuantity != Required
                || CustodyMassGrams <= 0L
                || custodySourceStackIds.Count == 0
                || custodySourceStackIds.Any(string.IsNullOrWhiteSpace)
                || custodySourceStackIds.Distinct(StringComparer.Ordinal).Count()
                    != custodySourceStackIds.Count
                || !custodySourceStackIds.SequenceEqual(
                    custodySourceStackIds.OrderBy(
                        value => value,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' custody provenance is invalid.");
            }

            ReturnOperationId = source.returnOperationId ?? string.Empty;
            ReturnReasonCode = source.returnReasonCode ?? string.Empty;
            ReturnPosition = new Vector2Int(source.returnX, source.returnY);
            returnOutputCommitIds = (source.returnOutputCommitIds
                    ?? new List<string>())
                .ToList();
            ReturnQuantity = source.returnQuantity;
            ReturnMassGrams = source.returnMassGrams;
            ConsumedOrLostMassGrams = source.consumedOrLostMassGrams;
            returnedCosts = (source.returnedCosts
                    ?? new List<OffenseSupplyPackingItemStateData>())
                .Where(value => value != null)
                .ToDictionary(
                    value => value.itemId,
                    value => value.amount,
                    StringComparer.Ordinal);
            ValidateReturnState();
        }

        private void ValidateReturnState()
        {
            if (Phase == OffenseSupplyCustodyPhase.CustodyOwned)
            {
                if (ReturnOperationId.Length != 0
                    || ReturnReasonCode.Length != 0
                    || ReturnPosition != default
                    || returnedCosts.Count != 0
                    || returnOutputCommitIds.Count != 0
                    || ReturnQuantity != 0
                    || ReturnMassGrams != 0L
                    || ConsumedOrLostMassGrams != 0L)
                {
                    throw new InvalidOperationException(
                        $"Owned expedition supply package '{PackageId}' contains terminal provenance.");
                }
                return;
            }
            if (Phase == OffenseSupplyCustodyPhase.Lost)
            {
                if (ReturnOperationId.Length != 0
                    || ReturnReasonCode.Length != 0
                    || ReturnPosition != default
                    || returnedCosts.Count != 0
                    || returnOutputCommitIds.Count != 0
                    || ReturnQuantity != 0
                    || ReturnMassGrams != 0L
                    || ConsumedOrLostMassGrams != CustodyMassGrams)
                {
                    throw new InvalidOperationException(
                        $"Lost expedition supply package '{PackageId}' has invalid mass provenance.");
                }
                return;
            }
            if (!string.Equals(
                    ReturnOperationId,
                    FormatReturnOperationId(PackageId),
                    StringComparison.Ordinal)
                || !string.Equals(
                    ReturnReasonCode,
                    ReturnSourceReasonCode,
                    StringComparison.Ordinal)
                || returnedCosts.Any(pair => pair.Value <= 0
                    || !Costs.TryGetValue(pair.Key, out int owned)
                    || pair.Value > owned))
            {
                throw new InvalidOperationException(
                    $"Expedition supply package '{PackageId}' return intent is invalid.");
            }
            if (Phase == OffenseSupplyCustodyPhase.ReturnPublishing)
            {
                if (returnOutputCommitIds.Count != 0
                    || ReturnQuantity != 0
                    || ReturnMassGrams != 0L
                    || ConsumedOrLostMassGrams != 0L)
                {
                    throw new InvalidOperationException(
                        $"Pending expedition supply return '{PackageId}' contains terminal output provenance.");
                }
                return;
            }
            int expectedQuantity = returnedCosts.Values.Sum();
            if (ReturnQuantity != expectedQuantity
                || ReturnMassGrams < 0L
                || ConsumedOrLostMassGrams < 0L
                || checked(ReturnMassGrams + ConsumedOrLostMassGrams)
                    != CustodyMassGrams
                || (ReturnQuantity == 0
                    ? returnOutputCommitIds.Count != 0
                    : returnOutputCommitIds.Count != returnedCosts.Count))
            {
                throw new InvalidOperationException(
                    $"Returned expedition supply package '{PackageId}' has invalid quantity/mass closure.");
            }
        }

        private static bool HasEmptyCustody(
            OffenseSupplyPackingStateData source) =>
            string.IsNullOrEmpty(source.custodyOperationId)
            && string.IsNullOrEmpty(source.custodyReasonCode)
            && string.IsNullOrEmpty(source.custodyCommitId)
            && (source.custodySourceStackIds?.Count ?? 0) == 0
            && source.custodyQuantity == 0
            && source.custodyMassGrams == 0L
            && !source.custodyAcknowledged
            && string.IsNullOrEmpty(source.returnOperationId)
            && string.IsNullOrEmpty(source.returnReasonCode)
            && source.returnX == 0
            && source.returnY == 0
            && (source.returnOutputCommitIds?.Count ?? 0) == 0
            && source.returnQuantity == 0
            && source.returnMassGrams == 0L
            && source.consumedOrLostMassGrams == 0L
            && (source.returnedCosts?.Count ?? 0) == 0;
    }
}
