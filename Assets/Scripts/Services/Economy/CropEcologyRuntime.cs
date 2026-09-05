using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IPhysicalSeedLotGateway
{
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    bool CanSpawnSeedLot(
        string seedItemId,
        int amount,
        Vector2Int position,
        out DomainFailure failure);
    bool RequestBestSeedLot(
        string seedItemId,
        string cropId,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);
    bool TryReleaseUnreachableSeedDelivery(
        string seedItemId,
        string cropId,
        Vector2Int destinationPosition,
        string destinationId,
        out bool released,
        out DomainFailure failure);
    bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
    bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);
    bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason);
    bool SpawnSeedLot(
        string seedItemId,
        int amount,
        SeedLotState seedLot,
        Vector2Int position);
}

public sealed class PhysicalSeedLotGateway : IPhysicalSeedLotGateway
{
    private const string UnreachableDeliveryReleaseReason =
        "seed-lot-delivery-unreachable-retry";
    private readonly IStockQuery stock;
    private readonly IItemTransferService transfers;
    private readonly IWorldItemStackRuntime physicalItems;
    private readonly IWorldItemDeliveryReachabilityQuery deliveryReachability;
    private readonly IFacilityBufferDestinationReleaseService destinationRelease;

    public PhysicalSeedLotGateway(
        IStockQuery stock,
        IItemTransferService transfers,
        IWorldItemStackRuntime physicalItems,
        IWorldItemDeliveryReachabilityQuery deliveryReachability,
        IFacilityBufferDestinationReleaseService destinationRelease)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.deliveryReachability = deliveryReachability
            ?? throw new ArgumentNullException(nameof(deliveryReachability));
        this.destinationRelease = destinationRelease
            ?? throw new ArgumentNullException(nameof(destinationRelease));
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        stock.GetAllStacks();

    public bool CanSpawnSeedLot(
        string seedItemId,
        int amount,
        Vector2Int position,
        out DomainFailure failure)
    {
        string normalized = seedItemId?.Trim() ?? string.Empty;
        if (amount <= 0 || string.IsNullOrWhiteSpace(normalized))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                normalized,
                amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }
        bool occupied = stock.GetAllStacks().Any(value => value != null
            && value.Quantity > 0
            && value.State == WorldItemStackState.Loose
            && string.IsNullOrWhiteSpace(value.DestinationId)
            && value.Position == position
            && string.Equals(value.ItemId, normalized, StringComparison.Ordinal));
        if (occupied)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputSpaceUnavailable,
                normalized,
                position.x.ToString(System.Globalization.CultureInfo.InvariantCulture),
                position.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool RequestBestSeedLot(
        string seedItemId,
        string cropId,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure)
    {
        requested = 0;
        failure = DomainFailure.None;
        WorldItemStackSnapshot[] candidates = stock.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && value.AvailableQuantity > 0
                && !value.Forbidden
                && value.State is WorldItemStackState.Loose or WorldItemStackState.Stored
                && string.Equals(value.ItemId, seedItemId, StringComparison.Ordinal))
            .Select(value => (stack: value, seed: TryDecode(value.Components)))
            .Where(value => value.seed != null
                && string.Equals(value.seed.cropId, cropId, StringComparison.Ordinal))
            .OrderBy(value => value.seed.pathogenLoad)
            .ThenByDescending(value => value.seed.generation)
            .ThenBy(value => value.stack.StackId, StringComparer.Ordinal)
            .Select(value => value.stack)
            .ToArray();
        bool reachabilityDeferred = false;
        DomainFailure firstTransferFailure = DomainFailure.None;
        foreach (WorldItemStackSnapshot candidate in candidates)
        {
            WorldItemDeliveryReachabilityStatus reachability = deliveryReachability
                .AssessExactStackDelivery(
                    (ItemStackId)candidate.StackId,
                    1,
                    destinationPosition,
                    destinationId,
                    out _);
            if (reachability == WorldItemDeliveryReachabilityStatus.Deferred)
            {
                reachabilityDeferred = true;
                continue;
            }
            if (reachability != WorldItemDeliveryReachabilityStatus.Reachable)
                continue;
            if (transfers.TryRequestStackDelivery(
                    (ItemStackId)candidate.StackId,
                    1,
                    destinationPosition,
                    destinationId,
                    out requested,
                    out failure))
            {
                return true;
            }
            if (!firstTransferFailure.IsFailure && failure.IsFailure)
                firstTransferFailure = failure;
        }
        requested = 0;
        if (firstTransferFailure.IsFailure)
        {
            failure = firstTransferFailure;
            return false;
        }
        failure = new DomainFailure(
            FailureCode.ItemTransferStackUnavailable,
            seedItemId,
            reachabilityDeferred
                ? "delivery-reachability-deferred"
                : "delivery-route-unreachable");
        return false;
    }

    public bool TryReleaseUnreachableSeedDelivery(
        string seedItemId,
        string cropId,
        Vector2Int destinationPosition,
        string destinationId,
        out bool released,
        out DomainFailure failure)
    {
        released = false;
        failure = DomainFailure.None;
        string destination = destinationId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(seedItemId)
            || string.IsNullOrWhiteSpace(cropId)
            || destination.Length == 0
            || !string.Equals(
                destination,
                destination.Trim(),
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "seed-delivery-recovery-identity-invalid");
            return false;
        }

        // Picked cargo has a durable delivery intent. Its actor owns replan and
        // physical recovery; releasing it here would violate carried-cargo
        // ownership and could teleport the item back to storage.
        if (physicalItems.CaptureHaulDeliveryIntentsByDestination(destination)
            .Any(intent => intent?.HasCommittedPickup == true))
        {
            return true;
        }

        WorldItemStackSnapshot[] pending = stock.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && !value.Forbidden
                && value.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                && string.Equals(
                    value.DestinationId,
                    destination,
                    StringComparison.Ordinal)
                && string.Equals(value.ItemId, seedItemId, StringComparison.Ordinal))
            .Select(value => (stack: value, seed: TryDecode(value.Components)))
            .Where(value => value.seed != null
                && string.Equals(value.seed.cropId, cropId, StringComparison.Ordinal))
            .Select(value => value.stack)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (pending.Length == 0)
            return true;

        foreach (WorldItemStackSnapshot candidate in pending)
        {
            WorldItemDeliveryReachabilityStatus reachability = deliveryReachability
                .AssessExactStackDelivery(
                    (ItemStackId)candidate.StackId,
                    1,
                    destinationPosition,
                    destination,
                    out _);
            if (reachability is WorldItemDeliveryReachabilityStatus.Reachable
                or WorldItemDeliveryReachabilityStatus.Deferred)
            {
                return true;
            }
        }

        if (!destinationRelease.TryReleaseAtOwnerPosition(
                destination,
                destinationPosition,
                UnreachableDeliveryReleaseReason,
                out int releasedQuantity,
                out string releaseFailure))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "seed-delivery-recovery-release-failed",
                releaseFailure);
            return false;
        }
        released = releasedQuantity > 0;
        return true;
    }

    public bool SpawnSeedLot(
        string seedItemId,
        int amount,
        SeedLotState seedLot,
        Vector2Int position) =>
        transfers.TrySpawnItemWithComponents(
            seedItemId,
            amount,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            new[] { SeedLotItemStateCodec.Encode(seedLot) },
            out int spawned)
        && spawned == amount;

    public bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) =>
        physicalItems.TryCommitPendingBatchPhysicalDisposition(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

    public bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt) =>
        physicalItems.TryGetPendingBatchPhysicalDisposition(operationId, out receipt);

    public bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason) =>
        physicalItems.AcknowledgeBatchPhysicalDisposition(
            commitId,
            out failureReason);

    private static SeedLotState TryDecode(IReadOnlyList<ItemInstanceComponentSaveData> components)
    {
        try { return SeedLotItemStateCodec.Decode(components); }
        catch { return null; }
    }

}

public sealed class CropEcologyRuntime :
    ICropEcologyService,
    ICropEcologyHarvestTransactionService,
    ICropEcologyPersistence,
    IInitialCropSeedGrant
{
    public const string GeneticsRandomStreamId = "crop:genetics";
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly CropGenomeDefinitionSO[] authoredGenomes;
    private readonly CropGenomeDefinitionSO[] baseGenomes;
    private readonly IRandomStream random;
    private readonly IStockQuery stock;
    private int version = 1;

    public CropEcologyRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        IGameContentCatalog content,
        IRandomStreamProvider randomStreams,
        IStockQuery stock)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        authoredGenomes = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<CropGenomeDefinitionSO>()
            .OrderBy(value => value.GenomeId, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> authoredCropIds = content
            .GetAll<CropDefinitionSO>()
            .Where(value => value != null)
            .Select(value => value.CropId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        baseGenomes = authoredGenomes
            .Where(value => value.GenomeId.EndsWith(
                ":base",
                StringComparison.Ordinal))
            .ToArray();
        if (authoredCropIds.Count != 12
            || authoredGenomes.Length != 32
            || authoredGenomes.Any(value => string.IsNullOrWhiteSpace(value.GenomeId)
                || string.IsNullOrWhiteSpace(value.CropId)
                || !authoredCropIds.Contains(value.CropId))
            || authoredGenomes.Select(value => value.GenomeId)
                .Distinct(StringComparer.Ordinal).Count() != 32
            || baseGenomes.Length != 12
            || !baseGenomes.Select(value => value.CropId)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(authoredCropIds))
            throw new InvalidOperationException(
                "V22 requires 12 crops, 32 valid authored genomes, and exactly one base genome per crop.");
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(GeneticsRandomStreamId);
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
    }

    public int Version => version;
    public IReadOnlyList<CropEcologyPlotSaveData> Plots => Current.Plots;
    public void Sow(string plotId, CropFamilyGroup group, SeedLotState seed)
    {
        Writable.Sow(plotId, group, seed);
        version = unchecked(version + 1);
    }
    public bool AdvanceDay(string plotId, bool lethalTemperature)
    {
        bool alive = Writable.AdvanceDay(plotId, lethalTemperature, () => random.NextFloat());
        version = unchecked(version + 1);
        return alive;
    }
    public CropGenomePhenotype GetPhenotype(string plotId) => Current.GetPhenotype(plotId);
    public CropHarvestEcologyResult Harvest(string plotId)
    {
        CropHarvestEcologyResult result = Writable.Harvest(
            plotId,
            () => random.NextFloat(),
            ResolvePhysicalGenomeReferences());
        version = unchecked(version + 1);
        return result;
    }
    public CropEcologyPreparedHarvestSnapshot PrepareHarvest(
        string operationId,
        string plotId)
    {
        CropEcologyPreparedHarvestSnapshot result = Writable.PrepareHarvest(
            operationId,
            plotId,
            () => random.NextFloat());
        version = unchecked(version + 1);
        return result;
    }
    public CropEcologyPreparedHarvestSnapshot CommitPreparedHarvest(
        string operationId)
    {
        CropEcologyPreparedHarvestSnapshot result = Writable
            .CommitPreparedHarvest(
                operationId,
                ResolvePhysicalGenomeReferences());
        version = unchecked(version + 1);
        return result;
    }
    public bool AcknowledgePreparedHarvest(string operationId)
    {
        bool removed = Writable.AcknowledgePreparedHarvest(operationId);
        if (removed) version = unchecked(version + 1);
        return removed;
    }
    public bool AbortPreparedHarvest(string operationId)
    {
        bool removed = Writable.AbortPreparedHarvest(operationId);
        if (removed) version = unchecked(version + 1);
        return removed;
    }
    public bool TryGetPreparedHarvest(
        string operationId,
        out CropEcologyPreparedHarvestSnapshot snapshot) =>
        Current.TryGetPreparedHarvest(operationId, out snapshot);
    public IReadOnlyList<CropEcologyPreparedHarvestSnapshot>
        CapturePreparedHarvests() => Current.CapturePreparedHarvests();
    public void ApplyCompost(string plotId)
    {
        Writable.ApplyCompost(plotId);
        version = unchecked(version + 1);
    }
    public void ApplyPestControl(string plotId, float amount)
    {
        Writable.ApplyPestControl(plotId, amount);
        version = unchecked(version + 1);
    }
    public void ApplyFungicide(string plotId, float amount)
    {
        Writable.ApplyFungicide(plotId, amount);
        version = unchecked(version + 1);
    }
    public bool AbandonPlot(string plotId)
    {
        bool removed = Writable.AbandonPlot(plotId);
        if (removed) version = unchecked(version + 1);
        return removed;
    }
    public CropEcologyWorldSaveData Capture() => Current.Capture();
    public bool TryClaim(out IReadOnlyList<SeedLotState> seedLots)
    {
        bool claimed = Writable.TryClaimInitialSeedGrant(out seedLots);
        if (claimed) version = unchecked(version + 1);
        return claimed;
    }
    public CropEcologyAggregateState PrepareRestore(CropEcologyWorldSaveData data)
    {
        CropEcologyAggregateState candidate = CropEcologyAggregateState.Restore(data);
        foreach (CropGenomeDefinitionSO definition in authoredGenomes)
            candidate.RegisterBaseGenome(definition.CreateRuntimeDefinition());
        return candidate;
    }
    public void PublishRestore(CropEcologyAggregateState candidate)
    {
        rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
        version = unchecked(version + 1);
    }

    private CropEcologyAggregateState Current => rootStore.GetOrCreate(CreateFresh);
    private CropEcologyAggregateState Writable => rootStore.GetOrCreateWritable(
        CreateFresh,
        value => CropEcologyAggregateState.Restore(value.Capture()));
    private CropEcologyAggregateState CreateFresh()
    {
        CropEcologyAggregateState state = new();
        foreach (CropGenomeDefinitionSO definition in authoredGenomes)
            state.RegisterBaseGenome(definition.CreateRuntimeDefinition());
        return state;
    }

    private IReadOnlyCollection<string> ResolvePhysicalGenomeReferences()
    {
        HashSet<string> references = new(StringComparer.Ordinal);
        foreach (WorldItemStackSnapshot stack in stock.GetAllStacks())
        {
            if (stack == null || stack.Quantity <= 0) continue;
            try
            {
                SeedLotState seed = SeedLotItemStateCodec.Decode(stack.Components);
                references.Add(seed.cultivarGenomeId);
            }
            catch (InvalidOperationException)
            {
                // Non-seed physical stacks intentionally have no seed-lot component.
            }
        }
        return references;
    }
}

public sealed class CropSeedBootstrapRuntime : VContainer.Unity.IInitializable
{
    private readonly IInitialCropSeedGrant grant;
    private readonly IPhysicalSeedLotGateway seedLots;
    private readonly IResourceEconomyContentCatalog content;

    public CropSeedBootstrapRuntime(
        IInitialCropSeedGrant grant,
        IPhysicalSeedLotGateway seedLots,
        IResourceEconomyContentCatalog content)
    {
        this.grant = grant ?? throw new ArgumentNullException(nameof(grant));
        this.seedLots = seedLots ?? throw new ArgumentNullException(nameof(seedLots));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public void Initialize()
    {
        if (!grant.TryClaim(out IReadOnlyList<SeedLotState> initial)) return;
        foreach (SeedLotState seed in initial)
        {
            CropDefinitionSO crop = content.Crops.Single(value =>
                string.Equals(value.CropId, seed.cropId, StringComparison.Ordinal));
            if (!seedLots.SpawnSeedLot(crop.SeedItemId, 4, seed, Vector2Int.zero))
                throw new InvalidOperationException(
                    $"Initial physical seed grant failed for crop '{seed.cropId}'.");
        }
    }
}
