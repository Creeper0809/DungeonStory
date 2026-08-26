using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

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
    ProtectedRouteCustody = 8
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
        int outputQuantity)
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
public sealed class PhysicalItemTransformService : IPhysicalItemTransformService
{
    private readonly WorldItemRepository repository;
    private readonly IWorldItemSpawner spawner;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IItemMarkerPresenter markerPresenter;

    public PhysicalItemTransformService(
        WorldItemRepository repository,
        IWorldItemSpawner spawner,
        IPhysicalItemMassQuery massQuery,
        IDungeonItemCatalogProvider catalog,
        IItemMarkerPresenter markerPresenter)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
    }

    public bool TryTransformWholeStack(
        string sourceStackId,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId,
        string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason)
    {
        if (!repository.RecordsById.TryGetValue(
                sourceStackId ?? string.Empty,
                out WorldItemStackRecord source)
            || source == null)
        {
            receipt = default;
            return Fail(
                PhysicalItemTransformFailureCode.SourceUnavailable,
                $"Transform source '{sourceStackId ?? string.Empty}' is unavailable.",
                out failureCode,
                out failureReason);
        }

        return TryTransformQuantity(
            sourceStackId,
            source.quantity,
            outputs,
            operationId,
            reasonCode,
            out receipt,
            out failureCode,
            out failureReason);
    }

    public bool TryTransformQuantity(
        string sourceStackId,
        int sourceQuantity,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId,
        string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason)
    {
        return TryTransformQuantities(
            new[] { new PhysicalItemTransformInput(sourceStackId, sourceQuantity) },
            outputs,
            operationId,
            reasonCode,
            out receipt,
            out failureCode,
            out failureReason);
    }

    public bool TryTransformQuantities(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId,
        string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        failureCode = PhysicalItemTransformFailureCode.None;
        failureReason = string.Empty;
        string operation = operationId ?? string.Empty;
        string reason = reasonCode ?? string.Empty;
        if (operation.Length == 0
            || reason.Length == 0
            || !string.Equals(operation, operation.Trim(), StringComparison.Ordinal)
            || !string.Equals(reason, reason.Trim(), StringComparison.Ordinal))
        {
            return Fail(
                PhysicalItemTransformFailureCode.InvalidRequest,
                "Transform identifiers must be non-empty canonical values.",
                out failureCode,
                out failureReason);
        }

        PhysicalItemTransformInput[] requestedInputs = (inputs
                ?? Array.Empty<PhysicalItemTransformInput>())
            .ToArray();
        PhysicalItemTransformOutput[] requestedOutputs = (outputs
                ?? Array.Empty<PhysicalItemTransformOutput>())
            .ToArray();
        if (requestedInputs.Length == 0
            || requestedInputs.Any(input => !input.IsValid)
            || requestedInputs.Select(input => input.StackId)
                .Distinct(StringComparer.Ordinal).Count() != requestedInputs.Length
            || requestedOutputs.Length == 0
            || requestedOutputs.Any(output => !output.IsValid))
        {
            return Fail(
                PhysicalItemTransformFailureCode.InvalidRequest,
                "A physical transform requires positive input and only valid outputs.",
                out failureCode,
                out failureReason);
        }

        PhysicalItemTransformOutput[] normalizedOutputs = requestedOutputs
            .GroupBy(
                output => new OutputKey(
                    output.ItemId,
                    output.Position,
                    output.State,
                    output.DestinationId),
                OutputKeyComparer.Instance)
            .Select(group => new PhysicalItemTransformOutput(
                group.Key.ItemId,
                checked(group.Sum(output => output.Quantity)),
                group.Key.Position,
                group.Key.State,
                group.Key.DestinationId))
            .OrderBy(output => output.ItemId, StringComparer.Ordinal)
            .ThenBy(output => output.Position.y)
            .ThenBy(output => output.Position.x)
            .ThenBy(output => (int)output.State)
            .ThenBy(output => output.DestinationId, StringComparer.Ordinal)
            .ToArray();
        List<SourceMutation> sourceMutations = new(requestedInputs.Length);
        foreach (PhysicalItemTransformInput input in requestedInputs
                     .OrderBy(candidate => candidate.StackId, StringComparer.Ordinal))
        {
            if (!repository.RecordsById.TryGetValue(
                    input.StackId,
                    out WorldItemStackRecord source)
                || source == null
                || source.quantity < input.Quantity
                || source.quantity - source.reservedQuantity < input.Quantity
                || source.state is WorldItemStackState.Carried
                    or WorldItemStackState.InTransit)
            {
                return Fail(
                    PhysicalItemTransformFailureCode.SourceUnavailable,
                    $"Transform source '{input.StackId}' is unavailable.",
                    out failureCode,
                    out failureReason);
            }
            if (source.reservedQuantity > 0
                || !string.IsNullOrEmpty(source.reservedByPersistentId))
            {
                return Fail(
                    PhysicalItemTransformFailureCode.SourceReserved,
                    $"Transform source '{input.StackId}' is reserved.",
                    out failureCode,
                    out failureReason);
            }
            if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    source.components))
            {
                return Fail(
                    PhysicalItemTransformFailureCode.ProtectedRouteCustody,
                    $"Transform source '{input.StackId}' retains prepared-output route custody.",
                    out failureCode,
                    out failureReason);
            }

            sourceMutations.Add(new SourceMutation(source, input.Quantity));
        }
        HashSet<string> sourceItemIds = sourceMutations
            .Select(mutation => mutation.Record.itemId)
            .ToHashSet(StringComparer.Ordinal);
        if (normalizedOutputs.Any(output => sourceItemIds.Contains(output.ItemId)))
        {
            return Fail(
                PhysicalItemTransformFailureCode.InvalidRequest,
                "Transform output cannot reuse the source item identity.",
                out failureCode,
                out failureReason);
        }

        long outputMassGrams = 0L;
        int outputQuantity = 0;
        foreach (PhysicalItemTransformOutput output in normalizedOutputs)
        {
            if (!catalog.TryGetDefinition(output.ItemId, out DungeonItemDefinition definition)
                || definition == null
                || definition.MaxStack <= 0)
            {
                return Fail(
                    PhysicalItemTransformFailureCode.OutputDefinitionMissing,
                    $"Transform output definition '{output.ItemId}' is missing.",
                    out failureCode,
                    out failureReason);
            }
            if (PhysicalItemIds.TryGetEquipmentDefinitionId(output.ItemId, out _)
                || PhysicalItemIds.IsEquipmentModule(output.ItemId))
            {
                return Fail(
                    PhysicalItemTransformFailureCode.OutputRequiresInstanceAuthority,
                    $"Transform output '{output.ItemId}' requires instance authority.",
                    out failureCode,
                    out failureReason);
            }

            outputMassGrams = checked(outputMassGrams
                + massQuery.GetDefinitionUnitMass((ItemDefinitionId)output.ItemId)
                    .Multiply(output.Quantity).Value);
            outputQuantity = checked(outputQuantity + output.Quantity);
        }

        long inputMassGrams = 0L;
        int inputQuantity = 0;
        foreach (SourceMutation mutation in sourceMutations)
        {
            WorldItemStackRecord source = mutation.Record;
            PhysicalItemMassSubject sourceSubject = PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)source.itemId,
                source.itemInstanceId,
                source.components);
            inputMassGrams = checked(inputMassGrams + massQuery.GetQuantityMass(
                (ItemDefinitionId)source.itemId,
                sourceSubject,
                mutation.ConsumedQuantity).Value);
            inputQuantity = checked(inputQuantity + mutation.ConsumedQuantity);
        }
        if (outputMassGrams > inputMassGrams)
        {
            return Fail(
                PhysicalItemTransformFailureCode.OutputMassExceedsInput,
                $"Transform '{operation}' creates {outputMassGrams - inputMassGrams}g.",
                out failureCode,
                out failureReason);
        }

        HashSet<Vector2Int> changedPositions = normalizedOutputs
            .Select(output => output.Position)
            .Concat(sourceMutations.Select(mutation => mutation.Record.position))
            .ToHashSet();
        Dictionary<string, int> quantitiesBefore = changedPositions
            .SelectMany(position => repository.RecordsByPosition.TryGetValue(
                    position,
                    out List<WorldItemStackRecord> records)
                ? records
                : Enumerable.Empty<WorldItemStackRecord>())
            .Where(record => record != null)
            .Distinct()
            .ToDictionary(record => record.stackId, record => record.quantity, StringComparer.Ordinal);

        try
        {
            foreach (PhysicalItemTransformOutput output in normalizedOutputs)
            {
                int spawned = spawner.Spawn(
                    output.ItemId,
                    output.Quantity,
                    output.Position,
                    output.State,
                    output.DestinationId);
                if (spawned != output.Quantity)
                {
                    RollbackOutputs(quantitiesBefore, changedPositions);
                    return Fail(
                        PhysicalItemTransformFailureCode.OutputCommitFailed,
                        $"Transform output '{output.ItemId}' committed {spawned}/{output.Quantity}.",
                        out failureCode,
                        out failureReason);
                }
            }

            foreach (SourceMutation mutation in sourceMutations)
            {
                if (mutation.Record.quantity == mutation.ConsumedQuantity)
                {
                    repository.Remove(mutation.Record);
                    mutation.Removed = true;
                }
                else
                {
                    mutation.Record.quantity = checked(
                        mutation.Record.quantity - mutation.ConsumedQuantity);
                    repository.MarkChanged();
                }
            }
            foreach (Vector2Int position in changedPositions)
            {
                markerPresenter.RefreshAt(position);
            }
            receipt = new PhysicalItemTransformReceipt(
                operation,
                reason,
                sourceMutations.Select(mutation => mutation.Record.stackId).ToArray(),
                inputQuantity,
                inputMassGrams,
                outputMassGrams,
                outputQuantity);
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                RollbackOutputs(quantitiesBefore, changedPositions);
            }
            finally
            {
                RollbackSources(sourceMutations);
            }
            return Fail(
                PhysicalItemTransformFailureCode.OutputCommitFailed,
                $"Transform '{operation}' rolled back: {exception.Message}",
                out failureCode,
                out failureReason);
        }
    }

    private void RollbackSources(IReadOnlyList<SourceMutation> sources)
    {
        foreach (SourceMutation source in sources)
        {
            if (source.Removed)
            {
                repository.Add(source.Record);
            }
            else
            {
                source.Record.quantity = source.OriginalQuantity;
            }
        }
        repository.MarkChanged();
    }

    private void RollbackOutputs(
        IReadOnlyDictionary<string, int> quantitiesBefore,
        IReadOnlyCollection<Vector2Int> positions)
    {
        WorldItemStackRecord[] current = positions
            .SelectMany(position => repository.RecordsByPosition.TryGetValue(
                    position,
                    out List<WorldItemStackRecord> records)
                ? records.ToArray()
                : Array.Empty<WorldItemStackRecord>())
            .Where(record => record != null)
            .Distinct()
            .ToArray();
        foreach (WorldItemStackRecord record in current)
        {
            if (quantitiesBefore.TryGetValue(record.stackId, out int quantity))
            {
                record.quantity = quantity;
            }
            else
            {
                repository.Remove(record);
            }
        }
        repository.MarkChanged();
        foreach (Vector2Int position in positions)
        {
            markerPresenter.RefreshAt(position);
        }
    }

    private static bool Fail(
        PhysicalItemTransformFailureCode code,
        string reason,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason)
    {
        failureCode = code;
        failureReason = reason;
        return false;
    }

    private readonly struct OutputKey
    {
        internal OutputKey(
            string itemId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId)
        {
            ItemId = itemId;
            Position = position;
            State = state;
            DestinationId = destinationId ?? string.Empty;
        }

        internal string ItemId { get; }
        internal Vector2Int Position { get; }
        internal WorldItemStackState State { get; }
        internal string DestinationId { get; }
    }

    private sealed class SourceMutation
    {
        internal SourceMutation(WorldItemStackRecord record, int consumedQuantity)
        {
            Record = record;
            ConsumedQuantity = consumedQuantity;
            OriginalQuantity = record.quantity;
        }

        internal WorldItemStackRecord Record { get; }
        internal int ConsumedQuantity { get; }
        internal int OriginalQuantity { get; }
        internal bool Removed { get; set; }
    }

    private sealed class OutputKeyComparer : IEqualityComparer<OutputKey>
    {
        internal static readonly OutputKeyComparer Instance = new();

        public bool Equals(OutputKey left, OutputKey right) =>
            left.Position == right.Position
            && left.State == right.State
            && string.Equals(left.ItemId, right.ItemId, StringComparison.Ordinal)
            && string.Equals(
                left.DestinationId,
                right.DestinationId,
                StringComparison.Ordinal);

        public int GetHashCode(OutputKey value) => HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(value.ItemId),
            value.Position,
            (int)value.State,
            StringComparer.Ordinal.GetHashCode(value.DestinationId));
    }
}
