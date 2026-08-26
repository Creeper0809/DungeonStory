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
    bool TryEnsureSeedLotOutput(
        string seedItemId,
        SeedLotState seedLot,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        string operationId,
        out string commitId,
        out string failureReason);
    bool SpawnSeedLot(
        string seedItemId,
        int amount,
        SeedLotState seedLot,
        Vector2Int position);
}

public sealed class PhysicalSeedLotGateway : IPhysicalSeedLotGateway
{
    private readonly IStockQuery stock;
    private readonly IItemTransferService transfers;
    private readonly IWorldItemStackRuntime physicalItems;

    public PhysicalSeedLotGateway(
        IStockQuery stock,
        IItemTransferService transfers,
        IWorldItemStackRuntime physicalItems)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
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
        WorldItemStackSnapshot candidate = stock.GetAllStacks()
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
            .FirstOrDefault();
        if (candidate == null)
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable, seedItemId);
            return false;
        }
        return transfers.TryRequestStackDelivery(
            (ItemStackId)candidate.StackId,
            1,
            destinationPosition,
            destinationId,
            out requested,
            out failure);
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

    public bool TryEnsureSeedLotOutput(
        string seedItemId,
        SeedLotState seedLot,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        string operationId,
        out string commitId,
        out string failureReason)
    {
        commitId = string.Empty;
        failureReason = string.Empty;
        string itemId = seedItemId ?? string.Empty;
        string destination = destinationId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        if (!IsCanonical(itemId)
            || seedLot == null
            || !IsCanonical(operation)
            || state is not (WorldItemStackState.Loose
                or WorldItemStackState.FacilityOutputBuffer)
            || state == WorldItemStackState.FacilityOutputBuffer
                && !IsCanonical(destination)
            || state == WorldItemStackState.Loose && destination.Length != 0)
        {
            failureReason = "crop-seed-output-invalid-request";
            return false;
        }

        ItemInstanceComponentSaveData encoded;
        try
        {
            encoded = SeedLotItemStateCodec.Encode(seedLot);
        }
        catch (Exception exception)
        {
            failureReason = "crop-seed-output-state-invalid:"
                + exception.GetType().Name;
            return false;
        }
        string pathogen = seedLot.pathogenLoad.ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture);
        string expectedCommit =
            $"physical-source:{operation}:{itemId}:1:{seedLot.cropId}:"
            + $"{seedLot.cultivarGenomeId}:{seedLot.generation}:{pathogen}";
        WorldItemStackSnapshot[] existing = stock.GetAllStacks()
            .Where(stack => stack != null
                && ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    expectedCommit))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        if (existing.Length > 0)
        {
            if (existing.Length != 1
                || existing[0].Quantity != 1
                || !string.Equals(existing[0].ItemId, itemId, StringComparison.Ordinal)
                || existing[0].State != state
                || existing[0].Position != position
                || !string.Equals(
                    existing[0].DestinationId,
                    destination,
                    StringComparison.Ordinal)
                || !SeedLotEquals(TryDecode(existing[0].Components), seedLot))
            {
                failureReason = "crop-seed-output-existing-conflict";
                return false;
            }
            commitId = expectedCommit;
            return true;
        }

        if (!transfers.TrySpawnItemWithComponents(
                itemId,
                1,
                position,
                state,
                destination,
                new[]
                {
                    encoded,
                    ProductionOutputCommitComponentCodec.Create(expectedCommit)
                },
                out int spawned)
            || spawned != 1)
        {
            failureReason = "crop-seed-output-space-unavailable";
            return false;
        }
        WorldItemStackSnapshot[] published = stock.GetAllStacks()
            .Where(stack => stack != null
                && ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    expectedCommit))
            .ToArray();
        if (published.Length != 1
            || published[0].Quantity != 1
            || !string.Equals(published[0].ItemId, itemId, StringComparison.Ordinal)
            || published[0].State != state
            || published[0].Position != position
            || !string.Equals(
                published[0].DestinationId,
                destination,
                StringComparison.Ordinal)
            || !SeedLotEquals(TryDecode(published[0].Components), seedLot))
        {
            failureReason = "crop-seed-output-postcondition-failed";
            return false;
        }
        commitId = expectedCommit;
        return true;
    }

    private static SeedLotState TryDecode(IReadOnlyList<ItemInstanceComponentSaveData> components)
    {
        try { return SeedLotItemStateCodec.Decode(components); }
        catch { return null; }
    }

    private static bool SeedLotEquals(SeedLotState left, SeedLotState right) =>
        left != null
        && right != null
        && string.Equals(left.cropId, right.cropId, StringComparison.Ordinal)
        && string.Equals(
            left.cultivarGenomeId,
            right.cultivarGenomeId,
            StringComparison.Ordinal)
        && left.generation == right.generation
        && left.pathogenLoad.Equals(right.pathogenLoad);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class CropEcologyRuntime :
    ICropEcologyService,
    ICropEcologyPersistence,
    IInitialCropSeedGrant
{
    private const string MutationRandomStreamId = "crop:genetics";
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
            .Get(MutationRandomStreamId);
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
