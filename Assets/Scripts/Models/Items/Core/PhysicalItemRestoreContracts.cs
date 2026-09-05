using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum PhysicalItemDispositionKind
{
    Source = 0,
    Transfer = 1,
    Transform = 2,
    Sink = 3
}

public sealed class PhysicalItemRestoreCandidateDispositionSnapshot
{
    public PhysicalItemRestoreCandidateDispositionSnapshot(
        PhysicalItemDispositionKind kind, string operationId, string reasonCode,
        string requestFingerprint, IReadOnlyList<string> sourceStackIds,
        int quantity, long inputMassGrams, string commitId)
    {
        Kind = kind;
        OperationId = operationId;
        ReasonCode = reasonCode;
        RequestFingerprint = requestFingerprint;
        SourceStackIds = Array.AsReadOnly(new List<string>(sourceStackIds ?? Array.Empty<string>()).ToArray());
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        CommitId = commitId;
    }

    public PhysicalItemRestoreCandidateDispositionSnapshot(PhysicalItemBatchDispositionSaveData source)
        : this((PhysicalItemDispositionKind)(source ?? throw new ArgumentNullException(nameof(source))).kind,
            source.operationId, source.reasonCode, source.requestFingerprint,
            source.sourceStackIds, source.quantity, source.inputMassGrams, source.commitId) { }

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public string CommitId { get; }
}

public interface IPhysicalItemRestoreCandidateQuery
{
    bool IsCandidateAvailable { get; }
    IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions { get; }
    bool TryGetPendingBatchDisposition(string operationId, out PhysicalItemRestoreCandidateDispositionSnapshot disposition);
}

/// <summary>
/// Read-only detached current-format projection of Items-owned production
/// input-destination drain authority. Economy restore joins use this projection
/// before either aggregate publishes its staged state.
/// </summary>
public interface IProductionInputDestinationCustodyDrainRestoreCandidateQuery
{
    bool IsCandidateAvailable { get; }
    IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> Drains
    {
        get;
    }
    bool TryGetDrain(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData drain);
}

public sealed class PhysicalItemRestoreCandidateOutputSnapshot
{
    public PhysicalItemRestoreCandidateOutputSnapshot(
        string commitId,
        string stackId,
        string itemId,
        int quantity,
        long massGrams,
        WorldItemStackState state,
        Vector2Int position,
        string destinationId)
    {
        CommitId = commitId ?? string.Empty;
        StackId = stackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        Quantity = quantity;
        MassGrams = massGrams;
        State = state;
        Position = position;
        DestinationId = destinationId ?? string.Empty;
    }

    public string CommitId { get; }
    public string StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public WorldItemStackState State { get; }
    public Vector2Int Position { get; }
    public string DestinationId { get; }
}

public interface IPhysicalItemRestoreCandidateOutputQuery
{
    bool IsCandidateAvailable { get; }
    IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> CommittedOutputs
    {
        get;
    }
    bool TryGetCommittedOutput(
        string commitId,
        out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs);
}

public sealed class EmptyPhysicalItemRestoreCandidateOutputQuery :
    IPhysicalItemRestoreCandidateOutputQuery
{
    public static readonly EmptyPhysicalItemRestoreCandidateOutputQuery Instance =
        new();

    private EmptyPhysicalItemRestoreCandidateOutputQuery()
    {
    }

    public bool IsCandidateAvailable => true;
    public IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
        CommittedOutputs => Array.Empty<
            PhysicalItemRestoreCandidateOutputSnapshot>();

    public bool TryGetCommittedOutput(
        string commitId,
        out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs)
    {
        outputs = Array.Empty<PhysicalItemRestoreCandidateOutputSnapshot>();
        return false;
    }
}

public sealed class FacilityBufferPlannedOutputRestoreStackSnapshot
{
    private readonly IReadOnlyList<ItemInstanceComponentSaveData> components;

    public FacilityBufferPlannedOutputRestoreStackSnapshot(
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string outputLineId,
        int stackOrdinal,
        string stackId,
        string itemId,
        int quantity,
        long massGrams,
        string componentSignature,
        WorldItemStackState state,
        Vector2Int position,
        string destinationId,
        string itemInstanceId = "",
        IReadOnlyList<ItemInstanceComponentSaveData> components = null,
        string preparedComponentFingerprint = "")
    {
        BatchCommitId = batchCommitId ?? string.Empty;
        OutcomeFingerprint = outcomeFingerprint ?? string.Empty;
        PlannedOutputFingerprint = plannedOutputFingerprint ?? string.Empty;
        OutputLineId = outputLineId ?? string.Empty;
        StackOrdinal = stackOrdinal;
        StackId = stackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        Quantity = quantity;
        MassGrams = massGrams;
        ComponentSignature = componentSignature ?? string.Empty;
        State = state;
        Position = position;
        DestinationId = destinationId ?? string.Empty;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        this.components = Array.AsReadOnly((components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Select(value => value?.Clone()
                ?? throw new ArgumentException(
                    "Restore stack components cannot contain null.",
                    nameof(components)))
            .ToArray());
        PreparedComponentFingerprint = preparedComponentFingerprint
            ?? string.Empty;
    }

    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public string OutputLineId { get; }
    public int StackOrdinal { get; }
    public string StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string ComponentSignature { get; }
    public WorldItemStackState State { get; }
    public Vector2Int Position { get; }
    public string DestinationId { get; }
    public string ItemInstanceId { get; }
    public IReadOnlyList<ItemInstanceComponentSaveData> Components =>
        components ?? Array.Empty<ItemInstanceComponentSaveData>();
    public string PreparedComponentFingerprint { get; }
}

public sealed class FacilityBufferPlannedOutputRestoreBatchSnapshot
{
    private readonly IReadOnlyList<
        FacilityBufferPlannedOutputRestoreStackSnapshot> stacks;

    public FacilityBufferPlannedOutputRestoreBatchSnapshot(
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        int totalQuantity,
        long totalMassGrams,
        IReadOnlyList<FacilityBufferPlannedOutputRestoreStackSnapshot> stacks)
    {
        BatchCommitId = batchCommitId ?? string.Empty;
        OutcomeFingerprint = outcomeFingerprint ?? string.Empty;
        PlannedOutputFingerprint = plannedOutputFingerprint ?? string.Empty;
        TotalQuantity = totalQuantity;
        TotalMassGrams = totalMassGrams;
        this.stacks = Array.AsReadOnly(new List<
            FacilityBufferPlannedOutputRestoreStackSnapshot>(
            stacks ?? Array.Empty<
                FacilityBufferPlannedOutputRestoreStackSnapshot>()).ToArray());
    }

    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public int TotalQuantity { get; }
    public long TotalMassGrams { get; }
    public IReadOnlyList<FacilityBufferPlannedOutputRestoreStackSnapshot> Stacks =>
        stacks ?? Array.Empty<FacilityBufferPlannedOutputRestoreStackSnapshot>();
}

public interface IFacilityBufferPlannedOutputRestoreCandidateQuery
{
    bool IsCandidateAvailable { get; }
    IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> Batches { get; }
    bool TryGetBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch);
}

public interface IFacilityBufferAcknowledgedOutputRestoreCandidateQuery
{
    bool IsCandidateAvailable { get; }
    IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> Batches { get; }
    bool TryGetBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch);
}

public sealed class EmptyFacilityBufferPlannedOutputRestoreCandidateQuery :
    IFacilityBufferPlannedOutputRestoreCandidateQuery
{
    public static readonly EmptyFacilityBufferPlannedOutputRestoreCandidateQuery
        Instance = new();

    private EmptyFacilityBufferPlannedOutputRestoreCandidateQuery()
    {
    }

    public bool IsCandidateAvailable => true;
    public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> Batches =>
        Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>();
    public bool TryGetBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
    {
        batch = null;
        return false;
    }
}

public sealed class EmptyFacilityBufferAcknowledgedOutputRestoreCandidateQuery :
    IFacilityBufferAcknowledgedOutputRestoreCandidateQuery
{
    public static readonly
        EmptyFacilityBufferAcknowledgedOutputRestoreCandidateQuery Instance =
            new();

    private EmptyFacilityBufferAcknowledgedOutputRestoreCandidateQuery()
    {
    }

    public bool IsCandidateAvailable => true;
    public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> Batches =>
        Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>();

    public bool TryGetBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
    {
        batch = null;
        return false;
    }
}
