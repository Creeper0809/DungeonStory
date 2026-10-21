using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

internal sealed class CharacterBreakdownWorld
{
    private readonly IUiClock uiClock;
    internal sealed class FilthTransactionSnapshot
    {
        internal FilthTransactionSnapshot(
            IReadOnlyList<WorldFilthSaveData> filth,
            int nextSequence,
            WorldFilthTransactionSnapshot runtimeSnapshot)
        {
            Filth = filth ?? throw new ArgumentNullException(nameof(filth));
            NextSequence = nextSequence;
            RuntimeSnapshot = runtimeSnapshot;
        }

        internal IReadOnlyList<WorldFilthSaveData> Filth { get; }
        internal int NextSequence { get; }
        internal WorldFilthTransactionSnapshot RuntimeSnapshot { get; }
    }

    internal sealed class WaterTransactionSnapshot
    {
        internal WaterTransactionSnapshot(
            IReadOnlyList<WorldWaterSourceSaveData> sources,
            int nextSequence)
        {
            Sources = sources ?? throw new ArgumentNullException(nameof(sources));
            NextSequence = nextSequence;
        }

        internal IReadOnlyList<WorldWaterSourceSaveData> Sources { get; }
        internal int NextSequence { get; }
    }
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldFilthQuery filthQuery;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly Queue<string> pendingCorpsePublications = new();
    private readonly HashSet<string> queuedCorpsePublications = new(StringComparer.Ordinal);
    private int corpseRestoreRevision = -1;
    private float nextCorpseRecoveryAt;
    private string lastCorpseRecoveryFailure = string.Empty;

    public CharacterBreakdownWorld(
        IUiClock uiClock,
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IWorldFilthQuery filthQuery,
        IWorldWaterQuery waterQuery,
        IRoomLayoutCache roomLayoutCache,
        ICharacterAiWorldRegistry worldRegistry,
        IPhysicalItemBatchDispositionService batchDispositions = null,
        IDomainFactStagingPublisher facts = null,
        IDomainFactRecoveryPublisher recovery = null)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.filthQuery = filthQuery
            ?? throw new ArgumentNullException(nameof(filthQuery));
        this.waterQuery = waterQuery
            ?? throw new ArgumentNullException(nameof(waterQuery));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.batchDispositions = batchDispositions;
        this.facts = facts;
        this.recovery = recovery;
    }

    public IReadOnlyList<CharacterActor> Characters => worldRegistry.Characters;
    public IReadOnlyList<BuildableObject> Buildings => worldRegistry.Buildings;

    public bool TryGetGrid(out Grid grid)
    {
        return gridSystemProvider.TryGetGrid(out grid);
    }

    public bool TryFindBestAvailableStack(
        Vector2Int origin,
        Func<string, int> rankSelector,
        out WorldItemStackSnapshot stack)
    {
        return itemStackRuntime.TryFindBestAvailableStack(
            origin,
            rankSelector,
            out stack);
    }

    public bool TryConsumeStack(
        string stackId,
        int quantity,
        string operationId,
        string reasonCode,
        out WorldItemStackSnapshot consumed)
    {
        bool committed = itemStackRuntime.TryCommitPhysicalDisposition(
            stackId,
            quantity,
            PhysicalItemDispositionKind.Sink,
            operationId,
            reasonCode,
            out PhysicalItemDispositionReceipt receipt,
            out _);
        consumed = committed ? receipt.Consumed : null;
        return committed;
    }

    public DungeonPhysicalItemSaveData CaptureItems() =>
        itemStackRuntime.Capture();

    internal bool TryGetStack(string stackId, out WorldItemStackSnapshot stack)
    {
        foreach (WorldItemStackSnapshot candidate in itemStackRuntime.GetAllStacks())
        {
            if (candidate != null && candidate.StackId == stackId)
            {
                stack = candidate;
                return true;
            }
        }
        stack = null;
        return false;
    }

    internal bool TryConsumeCorpse(WorldItemStackSnapshot source, string operationId, string reasonCode,
        CharacterCorpseConsumptionExperienceData experience, CharacterDeprivationStateStore owner,
        out PhysicalItemBatchDispositionReceipt receipt, out string failureReason)
    {
        receipt = default;
        if (batchDispositions is not IJointPhysicalItemBatchDispositionService joined || facts == null || recovery == null)
        { failureReason = "corpse-consumption-fact-capability-unavailable"; return false; }
        bool committed = joined.TryCommitPending(new[] { new PhysicalItemTransformInput(source.StackId, 1) },
            PhysicalItemDispositionKind.Sink, operationId, reasonCode,
            new CorpseEffect(facts, owner, experience), out receipt, out failureReason);
        if (committed) QueueCorpseRecovery(operationId);
        return committed;
    }

    internal bool AcknowledgeCorpseConsumption(in PhysicalItemBatchDispositionReceipt receipt,
        CharacterDeprivationStateStore owner, out string failureReason)
    {
        if (!owner.TryGetCorpsePublication(receipt.OperationId, out var pending))
        { failureReason = "corpse-consumption-pending-fact-missing"; return false; }
        return RecoverCorpsePublication(owner, pending, out failureReason);
    }

    private bool RecoverCorpsePublication(CharacterDeprivationStateStore owner,
        CharacterCorpseConsumptionPublicationData pending, out string failure)
    {
        if (recovery == null || batchDispositions == null)
        { failure = "corpse-consumption-recovery-unavailable"; return false; }
        var fact = CharacterCorpseConsumedFact.FromSave(pending.fact);
        var deliveries = new DomainFactDelivery[1];
        if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) },
                deliveries, out failure)) return false;
        if (!deliveries[0].Completed || deliveries[0].OperationKey != pending.publicationKey)
        { failure = "corpse-consumption-publication-pending:" + deliveries[0].Detail; return false; }
        if (!batchDispositions.Acknowledge(fact.Physical.Receipt.CommitId, out failure)) return false;
        owner.ClearCorpsePublication(fact.Physical.Receipt.OperationId, pending.publicationKey);
        return true;
    }

    internal void RecoverCorpsePublications(CharacterDeprivationStateStore owner)
    {
        if (owner.IsRestoreStaging) return;
        if (corpseRestoreRevision != owner.PublishedRestoreRevision)
        {
            corpseRestoreRevision = owner.PublishedRestoreRevision;
            pendingCorpsePublications.Clear(); queuedCorpsePublications.Clear(); nextCorpseRecoveryAt = 0f;
            foreach (var pending in owner.CaptureCorpsePublications())
                QueueCorpseRecovery(pending.fact.physical.operationId);
        }
        if (pendingCorpsePublications.Count == 0 || uiClock.Time < nextCorpseRecoveryAt) return;
        nextCorpseRecoveryAt = uiClock.Time + 1f;
        int count = Math.Min(8, pendingCorpsePublications.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            string operation = pendingCorpsePublications.Dequeue(); queuedCorpsePublications.Remove(operation);
            if (!owner.TryGetCorpsePublication(operation, out var pending)) continue;
            try
            {
                if (RecoverCorpsePublication(owner, pending, out string detail)) continue;
                failure = detail;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            { failure = "corpse-consumption-recovery-failed:" + exception.GetType().Name; }
            QueueCorpseRecovery(operation);
        }
        if (!string.IsNullOrEmpty(failure) && failure != lastCorpseRecoveryFailure)
            Debug.LogWarning("Corpse consumption publication recovery pending: " + failure);
        lastCorpseRecoveryFailure = failure;
    }

    private void QueueCorpseRecovery(string operation)
    { if (queuedCorpsePublications.Add(operation)) pendingCorpsePublications.Enqueue(operation); }

    private sealed class CorpseEffect : IPhysicalItemDispositionEffect
    {
        private readonly IDomainFactStagingPublisher facts;
        private readonly CharacterDeprivationStateStore owner;
        private readonly CharacterCorpseConsumptionExperienceData experience;
        public CorpseEffect(IDomainFactStagingPublisher facts, CharacterDeprivationStateStore owner,
            CharacterCorpseConsumptionExperienceData experience)
        { this.facts = facts; this.owner = owner; this.experience = experience.Clone(); }
        public bool TryStage(in PhysicalItemBatchDispositionReceipt receipt, long revision,
            out IDomainFactStage stage, out string failure)
        {
            var fact = new CharacterCorpseConsumedFact(receipt, experience);
            if (!facts.TryStage(fact, out stage, out failure)) return false;
            owner.RecordCorpsePublication(new CharacterCorpseConsumptionPublicationData
            { publicationKey = stage.OperationKey, fact = fact.Snapshot() });
            return true;
        }
    }

    public void RestoreItems(DungeonPhysicalItemSaveData snapshot) =>
        itemStackRuntime.Restore(
            snapshot ?? throw new ArgumentNullException(nameof(snapshot)));

    public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(
        Vector2Int position,
        bool includeStored)
    {
        return itemStackRuntime.GetStacksAt(position, includeStored);
    }

    public WorldFilthSnapshot AddFilth(
        WorldFilthType type,
        Vector2Int position,
        float amount,
        string sourceId,
        float contamination,
        bool wallStain = false)
    {
        return filthQuery.AddFilth(
            type,
            position,
            amount,
            sourceId,
            contamination,
            wallStain);
    }

    public IReadOnlyList<WorldFilthSnapshot> GetFilthAt(Vector2Int position) =>
        filthQuery.GetAt(position);

    internal FilthTransactionSnapshot CaptureFilth()
    {
        if (filthQuery is IWorldFilthTransactionPort transactionPort)
        {
            return new FilthTransactionSnapshot(
                Array.Empty<WorldFilthSaveData>(),
                0,
                transactionPort.CaptureTransactionState());
        }

        return new FilthTransactionSnapshot(
            filthQuery.CaptureFilth(),
            filthQuery.NextFilthSequence,
            null);
    }

    internal void RestoreFilth(FilthTransactionSnapshot snapshot)
    {
        snapshot = snapshot
            ?? throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.RuntimeSnapshot != null)
        {
            if (filthQuery is not IWorldFilthTransactionPort transactionPort)
            {
                throw new InvalidOperationException(
                    "The world-filth transaction owner changed before rollback.");
            }
            transactionPort.RestoreTransactionState(snapshot.RuntimeSnapshot);
            return;
        }

        filthQuery.RestoreFilth(snapshot.Filth, snapshot.NextSequence);
    }

    public bool TryFindDrinkSource(
        Vector2Int origin,
        bool allowFoul,
        out WorldWaterSourceSnapshot source)
    {
        return waterQuery.TryFindDrinkSource(origin, allowFoul, out source);
    }

    public bool TryDrink(
        string sourceId,
        float amount,
        out WorldWaterQuality quality,
        out float consumed)
    {
        return waterQuery.TryDrink(sourceId, amount, out quality, out consumed);
    }

    public WaterTransactionSnapshot CaptureWater() => new(
        waterQuery.CaptureWaterSources(),
        waterQuery.NextWaterSequence);

    public void RestoreWater(WaterTransactionSnapshot snapshot)
    {
        snapshot = snapshot
            ?? throw new ArgumentNullException(nameof(snapshot));
        waterQuery.RestoreWaterSources(
            snapshot.Sources,
            snapshot.NextSequence);
    }

    public int GetAccidentLocationPriority(Grid grid, GridCell cell)
    {
        if (cell.AreaType == GridCellAreaType.ExteriorPath)
        {
            return 0;
        }

        if (cell.HasOccupantInLayer(GridLayer.Hallway))
        {
            return 100;
        }

        return roomLayoutCache.TryGetRoom(
                grid,
                cell.Position,
                out RoomInstance room)
            ? 200 + Mathf.RoundToInt(room.GetQualityScore() * 100f)
            : 350;
    }
}
