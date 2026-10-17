using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonStory.Foundation.Facts;

public enum PhysicalItemTransformFailureCode
{
    None = 0,
    InvalidRequest = 1,
    SourceUnavailable = 2,
    SourceReserved = 3,
    OutputDefinitionMissing = 4,
    OutputRequiresInstanceAuthority = 5,
    OutputMassExceedsInput = 6,
    OutputCommitFailed = 7,
    ProtectedRouteCustody = 8,
    OutcomeStateUnavailable = 9,
    OutcomeCommitFailed = 10
}

public readonly struct PhysicalItemTransformOutput
{
    public PhysicalItemTransformOutput(
        string itemId,
        int quantity,
        Vector2Int position,
        WorldItemStackState state = WorldItemStackState.Loose,
        string destinationId = "")
    {
        ItemId = itemId ?? string.Empty;
        Quantity = quantity;
        Position = position;
        State = state;
        DestinationId = destinationId ?? string.Empty;
    }

    public string ItemId { get; }
    public int Quantity { get; }
    public Vector2Int Position { get; }
    public WorldItemStackState State { get; }
    public string DestinationId { get; }
    public bool IsValid => ItemId.Length > 0
        && string.Equals(ItemId, ItemId.Trim(), StringComparison.Ordinal)
        && Quantity > 0;
}

public readonly struct PhysicalItemTransformInput
{
    public PhysicalItemTransformInput(string stackId, int quantity)
    {
        StackId = stackId ?? string.Empty;
        Quantity = quantity;
    }

    public string StackId { get; }
    public int Quantity { get; }
    public bool IsValid => StackId.Length > 0
        && string.Equals(StackId, StackId.Trim(), StringComparison.Ordinal)
        && Quantity > 0;
}

public readonly struct PhysicalItemTransformReceipt
{
    internal PhysicalItemTransformReceipt(
        string operationId,
        string reasonCode,
        IReadOnlyList<string> sourceStackIds,
        int inputQuantity,
        long inputMassGrams,
        long outputMassGrams,
        int outputQuantity, bool publicationPending = false, bool wasReplayed = false)
    {
        OperationId = operationId;
        ReasonCode = reasonCode;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>()).ToArray();
        SourceStackId = SourceStackIds.FirstOrDefault() ?? string.Empty;
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
        OutputMassGrams = outputMassGrams;
        LossMassGrams = checked(inputMassGrams - outputMassGrams);
        OutputQuantity = outputQuantity;
        PublicationPending = publicationPending;
        WasReplayed = wasReplayed;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string SourceStackId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
    public long OutputMassGrams { get; }
    public long LossMassGrams { get; }
    public int OutputQuantity { get; }
    public bool PublicationPending { get; }
    public bool WasReplayed { get; }
    public bool IsCommitted => OperationId?.Length > 0
        && SourceStackId?.Length > 0
        && SourceStackIds?.Count > 0
        && InputQuantity > 0
        && InputMassGrams > 0L
        && OutputMassGrams > 0L
        && LossMassGrams >= 0L
        && OutputQuantity > 0;
}

public interface IPhysicalItemTransformService
{
    bool TryTransformWholeStack(
        string sourceStackId,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId,
        string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason);

    bool TryTransformQuantity(
        string sourceStackId,
        int sourceQuantity,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId,
        string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason);

    bool TryTransformQuantities(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId,
        string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason);
}

/// <summary>
/// Narrow exact-once physical transform boundary. All source, output, and mass
/// checks complete before the source is removed. Output mutations are rolled
/// back if an unexpected generic spawn failure occurs, so the caller never
/// observes a deleted input with only a partial yield.
/// </summary>
public interface IPhysicalItemTransformBatchService
{
    bool TryTransformWholeStackWithCompanion(
        string sourceStackId, IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId, string reasonCode,
        DomainFactEmission companion, int companionAbsoluteDay,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode, out string failureReason);
}

public interface IPhysicalItemTransformRecoveryService
{
    bool HasTransformation(string operationId);
    bool TryRecoverTransformation(string operationId, DomainFactReplay companion,
        out PhysicalItemTransformReceipt receipt, out string failureReason);
}
