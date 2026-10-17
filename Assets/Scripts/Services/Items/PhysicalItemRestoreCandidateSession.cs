using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Detached cross-section restore views and their exact staged-candidate lifetime.
/// Live physical state remains owned by the repository and persistence service.
/// </summary>
internal sealed class PhysicalItemRestoreCandidateSession :
    IPhysicalItemRestoreCandidateQuery,
    IPhysicalEffectReceiptRestoreQuery,
    IPhysicalItemRestoreCandidateStackQuery,
    IProductionInputDestinationCustodyDrainRestoreCandidateQuery,
    IPhysicalItemRestoreCandidateOutputQuery,
    IFacilityBufferPlannedOutputRestoreCandidateQuery,
    IFacilityBufferAcknowledgedOutputRestoreCandidateQuery,
    IFacilityOutputExactRouteRestoreCandidateQuery,
    IFacilityOutputExactRouteDeliveryRevisionRestoreCandidateQuery,
    IDungeonRestoreTransactionParticipant,
    IDisposable
{
    private readonly IPhysicalItemMassQuery massQuery;
    private WorldItemRestoreState indexedState;
    private bool stageCommitActive;
    private IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
        restoreCandidateDispositions;
    private Dictionary<string, PhysicalEffectReceiptSaveData> restoreCandidateEffectReceipts;
    private Dictionary<string, WorldItemStackRecord>
        restoreCandidateStacksByInstanceId;
    private Dictionary<string, WorldItemStackRecord> restoreCandidateStacksById;
    private Dictionary<string, PhysicalItemRestoreCandidateDispositionSnapshot>
        restoreCandidateDispositionsByOperation;
    private IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
        restoreCandidateInputDestinationDrains;
    private Dictionary<string, ProductionInputDestinationCustodyDrainSaveData>
        restoreCandidateInputDestinationDrainsByStep;
    private IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
        restoreCandidateOutputs;
    private Dictionary<string,
        IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>>
        restoreCandidateOutputsByCommit;
    private IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        restoreCandidatePlannedOutputBatches;
    private Dictionary<string, FacilityBufferPlannedOutputRestoreBatchSnapshot>
        restoreCandidatePlannedOutputBatchesByCommit;
    private IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        restoreCandidateAcknowledgedOutputBatches;
    private Dictionary<string, FacilityBufferPlannedOutputRestoreBatchSnapshot>
        restoreCandidateAcknowledgedOutputBatchesByCommit;
    private IReadOnlyList<FacilityOutputExactRouteOutboxSaveData>
        restoreCandidateExactOutputRoutes;
    private Dictionary<string, FacilityOutputExactRouteOutboxSaveData>
        restoreCandidateExactOutputRoutesByOperation;
    private long restoreCandidateExactRouteCheckpointSequence;
    private string restoreCandidateExactRouteCheckpointDigest = string.Empty;
    private bool restoreCandidateLifetimeActive;
    private bool restoreCandidateLifetimePublished;


    internal PhysicalItemRestoreCandidateSession(IPhysicalItemMassQuery massQuery)
    {
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
    }

    internal void EnsureCanStage()
    {
        RequireIdleCommit();
        if (indexedState != null)
            throw new InvalidOperationException(
                "A physical-item restore candidate is already indexed.");
    }

    internal void BeginStageCommit(WorldItemRestoreState staged)
    {
        RequireIdleCommit();
        if (staged == null || !ReferenceEquals(indexedState, staged))
            throw new InvalidOperationException(
                "Physical-item restore stage no longer owns the indexed candidate.");
        stageCommitActive = true;
    }

    internal void EndStageCommit()
    {
        stageCommitActive = false;
    }

    internal void DiscardIfCurrent(WorldItemRestoreState staged)
    {
        // A late discard belongs only to its own candidate, including when a
        // different candidate is currently being committed.
        if (staged == null || !ReferenceEquals(indexedState, staged))
            return;
        ResetPhysicalRestoreCandidateLifetime();
    }

    private void RequireIdleCommit()
    {
        if (stageCommitActive)
            throw new InvalidOperationException(
                "Physical-item restore candidate cannot change during stage commit.");
    }

    public void Dispose()
    {
        ResetPhysicalRestoreCandidateLifetime();
    }

    public string ParticipantId =>
        "999.world.physical-item-restore-candidate-lifetime";

    public void BeginRestoreCandidate()
    {
        RequireIdleCommit();
        if (restoreCandidateLifetimeActive)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime is already active.");
        }
        if (!IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime requires a staged candidate index.");
        }

        restoreCandidateLifetimeActive = true;
        restoreCandidateLifetimePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        RequireIdleCommit();
        if (!restoreCandidateLifetimeActive
            || restoreCandidateLifetimePublished
            || !IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime is not ready to publish.");
        }

        restoreCandidateLifetimePublished = true;
    }

    public void RollbackPublishedRestoreCandidate() =>
        ResetPhysicalRestoreCandidateLifetime();

    public void CompleteRestoreCandidate()
    {
        RequireIdleCommit();
        if (!restoreCandidateLifetimeActive
            || !restoreCandidateLifetimePublished)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime cannot complete.");
        }

        ResetPhysicalRestoreCandidateLifetime();
    }

    public void DiscardRestoreCandidate() =>
        ResetPhysicalRestoreCandidateLifetime();

    private void ResetPhysicalRestoreCandidateLifetime()
    {
        RequireIdleCommit();
        ClearPhysicalRestoreCandidateIndex();
        restoreCandidateLifetimeActive = false;
        restoreCandidateLifetimePublished = false;
    }

    public bool IsCandidateAvailable => restoreCandidateDispositions != null;
    public bool IsPhysicalEffectReceiptCandidateAvailable => restoreCandidateEffectReceipts != null;

    public bool TryGetPhysicalEffectReceiptCandidate(PhysicalEffectReceiptKind kind, string operationId,
        out PhysicalEffectReceiptSaveData receipt)
    {
        receipt = null;
        if (restoreCandidateEffectReceipts == null || !restoreCandidateEffectReceipts.TryGetValue(
                PhysicalEffectReceiptRules.Key(kind, operationId), out var stored)) return false;
        receipt = stored.Clone();
        return true;
    }

    public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
        PendingBatchDispositions => restoreCandidateDispositions
            ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

    public bool TryGetPendingBatchDisposition(
        string operationId,
        out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
    {
        disposition = null;
        return restoreCandidateDispositionsByOperation != null
            && restoreCandidateDispositionsByOperation.TryGetValue(
                operationId ?? string.Empty,
                out disposition);
    }

    public bool TryGetStack(
        ItemInstanceId itemInstanceId,
        out PhysicalItemRestoreCandidateStackSnapshot stack)
    {
        stack = null;
        if (!IsCandidateAvailable
            || !itemInstanceId.IsValid
            || restoreCandidateStacksByInstanceId == null
            || !restoreCandidateStacksByInstanceId.TryGetValue(
                itemInstanceId.Value,
                out WorldItemStackRecord record))
        {
            return false;
        }

        stack = new PhysicalItemRestoreCandidateStackSnapshot(
            record.stackId,
            (ItemInstanceId)record.itemInstanceId,
            record.itemId,
            record.quantity,
            record.state,
            record.position,
            record.destinationId,
            record.forbidden);
        return true;
    }

    public bool TryGetStackById(string stackId,
        out PhysicalItemRestoreCandidateStackSnapshot stack)
    {
        stack = null;
        if (!IsCandidateAvailable || string.IsNullOrWhiteSpace(stackId)
            || restoreCandidateStacksById == null
            || !restoreCandidateStacksById.TryGetValue(stackId, out WorldItemStackRecord record))
            return false;
        stack = new PhysicalItemRestoreCandidateStackSnapshot(
            record.stackId, (ItemInstanceId)record.itemInstanceId, record.itemId,
            record.quantity, record.state, record.position, record.destinationId, record.forbidden);
        return true;
    }

    IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery.Drains =>
        (restoreCandidateInputDestinationDrains
            ?? Array.Empty<ProductionInputDestinationCustodyDrainSaveData>())
        .Select(value => value.Clone()).ToArray();

    bool IProductionInputDestinationCustodyDrainRestoreCandidateQuery.TryGetDrain(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData drain)
    {
        drain = null;
        if (restoreCandidateInputDestinationDrainsByStep == null
            || !restoreCandidateInputDestinationDrainsByStep.TryGetValue(
                stepOperationId ?? string.Empty,
                out ProductionInputDestinationCustodyDrainSaveData found))
        {
            return false;
        }
        drain = found.Clone();
        return true;
    }

    IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
        IPhysicalItemRestoreCandidateOutputQuery.CommittedOutputs =>
        restoreCandidateOutputs
        ?? Array.Empty<PhysicalItemRestoreCandidateOutputSnapshot>();

    bool IPhysicalItemRestoreCandidateOutputQuery.TryGetCommittedOutput(
        string commitId,
        out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs)
    {
        outputs = Array.Empty<PhysicalItemRestoreCandidateOutputSnapshot>();
        return restoreCandidateOutputsByCommit != null
            && restoreCandidateOutputsByCommit.TryGetValue(
                commitId ?? string.Empty,
                out outputs);
    }

    IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        IFacilityBufferPlannedOutputRestoreCandidateQuery.Batches =>
        restoreCandidatePlannedOutputBatches
        ?? Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>();

    bool IFacilityBufferPlannedOutputRestoreCandidateQuery.TryGetBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
    {
        batch = null;
        return restoreCandidatePlannedOutputBatchesByCommit != null
            && restoreCandidatePlannedOutputBatchesByCommit.TryGetValue(
                batchCommitId ?? string.Empty,
                out batch);
    }

    IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery.Batches =>
        restoreCandidateAcknowledgedOutputBatches
        ?? Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>();

    bool IFacilityBufferAcknowledgedOutputRestoreCandidateQuery.TryGetBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
    {
        batch = null;
        return restoreCandidateAcknowledgedOutputBatchesByCommit != null
            && restoreCandidateAcknowledgedOutputBatchesByCommit.TryGetValue(
                batchCommitId ?? string.Empty,
                out batch);
    }

    IReadOnlyList<FacilityOutputExactRouteOutboxSaveData>
        IFacilityOutputExactRouteRestoreCandidateQuery.Routes =>
        (restoreCandidateExactOutputRoutes
            ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
        .Select(value => value.Clone()).ToArray();

    IReadOnlyList<FacilityOutputExactRouteDeliveryRevisionSnapshot>
        IFacilityOutputExactRouteDeliveryRevisionRestoreCandidateQuery
            .CurrentDeliveryRevisions =>
        (restoreCandidateExactOutputRoutes
                ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Select(CreateDeliveryRevisionSnapshot)
            .ToArray();

    long IFacilityOutputExactRouteRestoreCandidateQuery
        .LastConfirmedCheckpointSequence =>
        restoreCandidateExactRouteCheckpointSequence;

    string IFacilityOutputExactRouteRestoreCandidateQuery
        .LastConfirmedCheckpointDigest =>
        restoreCandidateExactRouteCheckpointDigest;

    bool IFacilityOutputExactRouteRestoreCandidateQuery.TryGetRoute(
        string routeOperationId,
        out FacilityOutputExactRouteOutboxSaveData route)
    {
        route = null;
        if (restoreCandidateExactOutputRoutesByOperation == null
            || !restoreCandidateExactOutputRoutesByOperation.TryGetValue(
                routeOperationId ?? string.Empty,
                out FacilityOutputExactRouteOutboxSaveData stored))
            return false;
        route = stored.Clone();
        return true;
    }

    bool IFacilityOutputExactRouteDeliveryRevisionRestoreCandidateQuery
        .TryGetCurrentDeliveryRevision(
            string routeOperationId,
            out FacilityOutputExactRouteDeliveryRevisionSnapshot revision)
    {
        revision = null;
        if (restoreCandidateExactOutputRoutesByOperation == null
            || !restoreCandidateExactOutputRoutesByOperation.TryGetValue(
                routeOperationId ?? string.Empty,
                out FacilityOutputExactRouteOutboxSaveData route))
        {
            return false;
        }
        revision = CreateDeliveryRevisionSnapshot(route);
        return true;
    }

    private static FacilityOutputExactRouteDeliveryRevisionSnapshot
        CreateDeliveryRevisionSnapshot(
            FacilityOutputExactRouteOutboxSaveData route)
    {
        if (route == null)
            throw new InvalidOperationException(
                "Exact-route delivery revision source is null.");
        return new FacilityOutputExactRouteDeliveryRevisionSnapshot(
            route.routeOperationId,
            route.physicalReceiptFingerprint,
            route.currentDeliveryRevision,
            route.currentDeliveryRevisionFingerprint,
            route.currentDeliveryRerouteOperationId,
            route.currentTargetDestinationId,
            route.currentTargetPositionX,
            route.currentTargetPositionY,
            route.currentTargetAuthorityFingerprint);
    }

    internal void Index(WorldItemRestoreState staged)
    {
        EnsureCanStage();
        if (staged?.RepositoryState == null)
        {
            throw new ArgumentNullException(nameof(staged));
        }
        if (restoreCandidateDispositions != null
            || restoreCandidateStacksByInstanceId != null
            || restoreCandidateStacksById != null
            || restoreCandidateDispositionsByOperation != null
            || restoreCandidateInputDestinationDrains != null
            || restoreCandidateInputDestinationDrainsByStep != null
            || restoreCandidateOutputs != null
            || restoreCandidateOutputsByCommit != null
            || restoreCandidatePlannedOutputBatches != null
            || restoreCandidatePlannedOutputBatchesByCommit != null
            || restoreCandidateAcknowledgedOutputBatches != null
            || restoreCandidateAcknowledgedOutputBatchesByCommit != null
            || restoreCandidateExactOutputRoutes != null
            || restoreCandidateExactOutputRoutesByOperation != null)
        {
            throw new InvalidOperationException(
                "A physical-item restore candidate is already indexed.");
        }

        Dictionary<string, WorldItemStackRecord> stacksByInstanceId =
            new(StringComparer.Ordinal);
        Dictionary<string, WorldItemStackRecord> stacksById = new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord record in staged.RepositoryState.Records
                     .Where(value => value != null && value.quantity > 0))
        {
            if (!stacksById.TryAdd(record.stackId, record))
                throw new InvalidOperationException(
                    $"Duplicate physical restore candidate stack '{record.stackId}'.");
        }
        foreach (WorldItemStackRecord record in staged.RepositoryState.Records
                     .Where(value => value != null
                         && value.quantity > 0
                         && !string.IsNullOrWhiteSpace(value.itemInstanceId))
                     .OrderBy(value => value.itemInstanceId, StringComparer.Ordinal))
        {
            if (!stacksByInstanceId.TryAdd(
                    record.itemInstanceId,
                    record))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical restore candidate item instance '{record.itemInstanceId}'.");
            }
        }

        PhysicalItemRestoreCandidateDispositionSnapshot[] snapshots =
            staged.RepositoryState.PendingBatchDispositions.Values
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .Select(value =>
                    new PhysicalItemRestoreCandidateDispositionSnapshot(value))
                .ToArray();
        Dictionary<string, PhysicalItemRestoreCandidateDispositionSnapshot>
            byOperation = new(StringComparer.Ordinal);
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot snapshot in
                 snapshots)
        {
            if (!byOperation.TryAdd(snapshot.OperationId, snapshot))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical restore candidate operation '{snapshot.OperationId}'.");
            }
        }

        ProductionInputDestinationCustodyDrainSaveData[] inputDrains = staged
            .RepositoryState.PendingProductionInputDestinationDrains.Values
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
        Dictionary<string, ProductionInputDestinationCustodyDrainSaveData>
            inputDrainsByStep = new(StringComparer.Ordinal);
        foreach (ProductionInputDestinationCustodyDrainSaveData drain in inputDrains)
        {
            if (drain == null
                || !inputDrainsByStep.TryAdd(
                    drain.stepOperationId,
                    drain.Clone()))
            {
                throw new InvalidOperationException(
                    $"Duplicate production input-destination drain restore candidate '{drain?.stepOperationId}'.");
            }
        }

        PhysicalItemRestoreCandidateOutputSnapshot[] outputSnapshots =
            staged.RepositoryState.Records
                .Select(record => TryCreateCommittedOutputSnapshot(
                    record,
                    out PhysicalItemRestoreCandidateOutputSnapshot output)
                    ? output
                    : null)
                .Where(output => output != null)
                .OrderBy(output => output.CommitId, StringComparer.Ordinal)
                .ThenBy(output => output.StackId, StringComparer.Ordinal)
                .ToArray();
        Dictionary<string,
            IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>>
            outputsByCommit = outputSnapshots
                .GroupBy(output => output.CommitId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<
                        PhysicalItemRestoreCandidateOutputSnapshot>)
                        Array.AsReadOnly(group.ToArray()),
                    StringComparer.Ordinal);
        FacilityBufferPlannedOutputRestoreBatchSnapshot[] plannedBatches =
            FacilityBufferPlannedOutputRestoreCandidateFactory
            .CapturePendingBatches(
                staged.RepositoryState.Records,
                massQuery)
            .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, FacilityBufferPlannedOutputRestoreBatchSnapshot>
            plannedByCommit = new(StringComparer.Ordinal);
        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot planned in
                 plannedBatches)
        {
            if (!plannedByCommit.TryAdd(planned.BatchCommitId, planned))
            {
                throw new InvalidOperationException(
                    $"Duplicate planned-output restore batch '{planned.BatchCommitId}'.");
            }
        }
        FacilityBufferPlannedOutputRestoreBatchSnapshot[] acknowledgedBatches =
            FacilityBufferPlannedOutputRestoreCandidateFactory
            .CaptureAcknowledgedBatches(
                staged.RepositoryState.Records,
                massQuery)
            .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, FacilityBufferPlannedOutputRestoreBatchSnapshot>
            acknowledgedByCommit = new(StringComparer.Ordinal);
        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot acknowledged in
                 acknowledgedBatches)
        {
            if (!acknowledgedByCommit.TryAdd(
                    acknowledged.BatchCommitId,
                    acknowledged))
            {
                throw new InvalidOperationException(
                    $"Duplicate acknowledged-output restore batch '{acknowledged.BatchCommitId}'.");
            }
        }
        FacilityOutputExactRouteOutboxSaveData[] exactRoutes =
            (staged.ExactRouteCandidate?.Routes
                ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
        long exactRouteCheckpointSequence = staged.ExactRouteCandidate?
            .CheckpointSequence
            ?? throw new InvalidOperationException(
                "Physical restore has no exact-route checkpoint candidate.");
        string exactRouteCheckpointDigest = staged.ExactRouteCandidate
            .CheckpointDigest;
        Dictionary<string, FacilityOutputExactRouteOutboxSaveData>
            exactRoutesByOperation = new(StringComparer.Ordinal);
        foreach (FacilityOutputExactRouteOutboxSaveData route in exactRoutes)
        {
            if (route == null
                || !exactRoutesByOperation.TryAdd(
                    route.routeOperationId,
                    route))
            {
                throw new InvalidOperationException(
                    $"Duplicate exact-output-route restore candidate '{route?.routeOperationId}'.");
            }
        }
        var effectReceipts = staged.RepositoryState.PhysicalEffectReceipts.ToDictionary(
            value => value.Key, value => value.Value.Clone(), StringComparer.Ordinal);
        // Candidate publication must be a non-failing pointer/map swap. Keep
        // every potentially throwing projection and duplicate check above in
        // locals so a rejected candidate cannot leave a partially visible
        // cross-section restore index behind.
        restoreCandidateStacksByInstanceId = stacksByInstanceId;
        restoreCandidateEffectReceipts = effectReceipts;
        restoreCandidateStacksById = stacksById;
        restoreCandidateDispositions = Array.AsReadOnly(snapshots);
        restoreCandidateDispositionsByOperation = byOperation;
        restoreCandidateInputDestinationDrains = Array.AsReadOnly(inputDrains);
        restoreCandidateInputDestinationDrainsByStep = inputDrainsByStep;
        restoreCandidateOutputs = Array.AsReadOnly(outputSnapshots);
        restoreCandidateOutputsByCommit = outputsByCommit;
        restoreCandidatePlannedOutputBatches = Array.AsReadOnly(plannedBatches);
        restoreCandidatePlannedOutputBatchesByCommit = plannedByCommit;
        restoreCandidateAcknowledgedOutputBatches =
            Array.AsReadOnly(acknowledgedBatches);
        restoreCandidateAcknowledgedOutputBatchesByCommit =
            acknowledgedByCommit;
        restoreCandidateExactOutputRoutes = Array.AsReadOnly(exactRoutes);
        restoreCandidateExactOutputRoutesByOperation = exactRoutesByOperation;
        restoreCandidateExactRouteCheckpointSequence =
            exactRouteCheckpointSequence;
        restoreCandidateExactRouteCheckpointDigest =
            exactRouteCheckpointDigest;
        indexedState = staged;
    }

    private bool TryCreateCommittedOutputSnapshot(
        WorldItemStackRecord record,
        out PhysicalItemRestoreCandidateOutputSnapshot output)
    {
        output = null;
        ItemInstanceComponentSaveData commitComponent = (record?.components
                ?? new List<ItemInstanceComponentSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.ProductionOutputCommit,
                    StringComparison.Ordinal));
        ItemStateValueSaveData commitField = commitComponent?.values
            ?.SingleOrDefault(value => value != null
                && string.Equals(value.key, "commit-id", StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String);
        string commitId = commitField?.stringValue ?? string.Empty;
        if (record == null
            || record.quantity <= 0
            || string.IsNullOrWhiteSpace(commitId))
        {
            return false;
        }

        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)record.itemId,
            record.itemInstanceId,
            record.components);
        long massGrams = massQuery.GetQuantityMass(
            (ItemDefinitionId)record.itemId,
            subject,
            record.quantity).Value;
        output = new PhysicalItemRestoreCandidateOutputSnapshot(
            commitId,
            record.stackId,
            record.itemId,
            record.quantity,
            massGrams,
            record.state,
            record.position,
            record.destinationId);
        return true;
    }

    private void ClearPhysicalRestoreCandidateIndex()
    {
        indexedState = null;
        restoreCandidateEffectReceipts = null;
        restoreCandidateStacksByInstanceId = null;
        restoreCandidateStacksById = null;
        restoreCandidateDispositions = null;
        restoreCandidateDispositionsByOperation = null;
        restoreCandidateInputDestinationDrains = null;
        restoreCandidateInputDestinationDrainsByStep = null;
        restoreCandidateOutputs = null;
        restoreCandidateOutputsByCommit = null;
        restoreCandidatePlannedOutputBatches = null;
        restoreCandidatePlannedOutputBatchesByCommit = null;
        restoreCandidateAcknowledgedOutputBatches = null;
        restoreCandidateAcknowledgedOutputBatchesByCommit = null;
        restoreCandidateExactOutputRoutes = null;
        restoreCandidateExactOutputRoutesByOperation = null;
        restoreCandidateExactRouteCheckpointSequence = 0L;
        restoreCandidateExactRouteCheckpointDigest = string.Empty;
    }

}
