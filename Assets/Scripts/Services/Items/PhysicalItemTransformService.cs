using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using VContainer.Unity;
using UnityEngine;

public sealed class PhysicalItemTransformService : IPhysicalItemTransformService,
    IPhysicalItemTransformBatchService, IPhysicalItemTransformRecoveryService,
    ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly WorldItemRepository repository;
    private readonly IWorldItemSpawner spawner;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly Queue<string> pending = new();
    private readonly HashSet<string> queued = new(StringComparer.Ordinal);
    private float nextRecoveryTime;
    public string LastRecoveryFailure { get; private set; } = string.Empty;

    public PhysicalItemTransformService(
        WorldItemRepository repository,
        IWorldItemSpawner spawner,
        IPhysicalItemMassQuery massQuery,
        IDungeonItemCatalogProvider catalog,
        IItemMarkerPresenter markerPresenter,
        IGameSessionStateProvider gameDataProvider,
        IDomainFactPublisher facts)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        recovery = facts as IDomainFactRecoveryPublisher
            ?? throw new ArgumentException("Physical transformations require durable fact recovery.", nameof(facts));
    }

    public bool TryTransformWholeStack(
        string sourceStackId, IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId, string reasonCode, out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode, out string failureReason)
        => TransformWhole(sourceStackId, outputs, operationId, reasonCode, null, 0,
            out receipt, out failureCode, out failureReason);

    private bool TransformWhole(string sourceStackId,
        IReadOnlyList<PhysicalItemTransformOutput> outputs, string operationId, string reasonCode,
        DomainFactEmission companion, int companionAbsoluteDay,
        out PhysicalItemTransformReceipt receipt, out PhysicalItemTransformFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        int quantity;
        if (repository.TryGetPhysicalItemTransformation(operationId ?? string.Empty, out var prior))
            quantity = prior.fact.inputs.Count == 1 ? prior.fact.inputs[0].quantity : 0;
        else if (repository.RecordsById.TryGetValue(sourceStackId ?? string.Empty, out var source)
            && source != null)
            quantity = source.quantity;
        else
            return Fail(PhysicalItemTransformFailureCode.SourceUnavailable,
                "Physical transform source is unavailable.", out failureCode, out failureReason);
        return TryTransformQuantitiesCore(
            new[] { new PhysicalItemTransformInput(sourceStackId, quantity) },
            outputs, operationId, reasonCode, out receipt, out failureCode, out failureReason,
            companion, companionAbsoluteDay, true);
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

    public bool TryTransformWholeStackWithCompanion(
        string sourceStackId, IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId, string reasonCode, DomainFactEmission companion, int companionAbsoluteDay,
        out PhysicalItemTransformReceipt receipt, out PhysicalItemTransformFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        if (companion == null || !companion.Lease.IsValid)
            return Fail(PhysicalItemTransformFailureCode.OutcomeStateUnavailable,
                "Physical transform companion requires an active fact reservation.",
                out failureCode, out failureReason);
        return TransformWhole(sourceStackId, outputs, operationId, reasonCode,
            companion, companionAbsoluteDay, out receipt, out failureCode, out failureReason);
    }

    public bool TryTransformQuantities(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId,
        string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode,
        out string failureReason)
        => TryTransformQuantitiesCore(inputs, outputs, operationId, reasonCode,
            out receipt, out failureCode, out failureReason, null, 0, false);

    private bool TryTransformQuantitiesCore(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        IReadOnlyList<PhysicalItemTransformOutput> outputs,
        string operationId, string reasonCode,
        out PhysicalItemTransformReceipt receipt,
        out PhysicalItemTransformFailureCode failureCode, out string failureReason,
        DomainFactEmission companion, int companionAbsoluteDay, bool wholeStack)
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
        if (repository.TryGetPhysicalItemTransformation(operation, out var existing))
        {
            if (!MatchesRequest(existing.fact, reason, wholeStack, requestedInputs, normalizedOutputs)
                || !string.Equals(existing.companionPublicationKey,
                    companion?.Lease.PublicationKey ?? string.Empty, StringComparison.Ordinal))
                return Fail(PhysicalItemTransformFailureCode.InvalidRequest,
                    "Transform operation identity was reused with a different request or companion.",
                    out failureCode, out failureReason);
            bool recovered = TryRecoverTransformation(operation, null, out receipt, out failureReason);
            failureCode = recovered ? PhysicalItemTransformFailureCode.None
                : PhysicalItemTransformFailureCode.OutcomeCommitFailed;
            return recovered;
        }
        if (!repository.CanAddPhysicalItemTransformation(operation))
            return Fail(PhysicalItemTransformFailureCode.OutcomeStateUnavailable,
                "Physical transform journal capacity is exhausted.", out failureCode, out failureReason);
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

        if (!gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData?.day == null
            || gameData.day.Value < 0)
        {
            return Fail(
                PhysicalItemTransformFailureCode.OutcomeStateUnavailable,
                $"Transform '{operation}' cannot resolve the current run day.",
                out failureCode,
                out failureReason);
        }
        if (companion != null && companionAbsoluteDay != gameData.day.Value)
            return Fail(PhysicalItemTransformFailureCode.OutcomeStateUnavailable,
                "Physical transform and companion fact days differ.", out failureCode, out failureReason);
        var data = new PhysicalItemTransformationData
        {
            operationId = operation, reasonCode = reason, absoluteDay = gameData.day.Value,
            wholeStack = wholeStack, inputQuantity = inputQuantity, outputQuantity = outputQuantity,
            inputMassGrams = inputMassGrams, outputMassGrams = outputMassGrams,
            inputs = sourceMutations.Select(m => new PhysicalItemTransformationInputData
            {
                stackId = m.Record.stackId, itemId = m.Record.itemId,
                itemInstanceId = m.Record.itemInstanceId ?? string.Empty, quantity = m.ConsumedQuantity
            }).ToList(),
            outputs = normalizedOutputs.Select(o => new PhysicalItemTransformationOutputData
            {
                itemId = o.ItemId, quantity = o.Quantity, x = o.Position.x, y = o.Position.y,
                state = (int)o.State, destinationId = o.DestinationId
            }).ToList()
        };
        PhysicalItemTransformedFact fact;
        try { fact = new PhysicalItemTransformedFact(data); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Fail(PhysicalItemTransformFailureCode.InvalidRequest, exception.Message,
                out failureCode, out failureReason);
        }
        if (!facts.TryReserve(fact, out var lease, out var reserveFailure))
            return Fail(PhysicalItemTransformFailureCode.OutcomeCommitFailed, reserveFailure,
                out failureCode, out failureReason);
        var journal = new PhysicalItemTransformationSaveData
        {
            fact = fact.Snapshot(), publicationKey = lease.PublicationKey,
            companionPublicationKey = companion?.Lease.PublicationKey ?? string.Empty
        };

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

        bool mayBeCommitted = false;
        bool publicationStarted = false;
        try
        {
            // Owner state holds the exact frozen fact and opaque identities before publication.
            repository.AddPhysicalItemTransformation(journal);
            foreach (PhysicalItemTransformOutput output in normalizedOutputs)
            {
                int spawned = spawner.Spawn(output.ItemId, output.Quantity, output.Position,
                    output.State, output.DestinationId);
                if (spawned != output.Quantity)
                    throw new InvalidOperationException(
                        $"Transform output '{output.ItemId}' committed {spawned}/{output.Quantity}.");
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
                    mutation.Record.quantity = checked(mutation.Record.quantity - mutation.ConsumedQuantity);
                    repository.MarkChanged();
                }
            }
            publicationStarted = true;
            DomainFactDelivery[] deliveries;
            if (companion == null)
                deliveries = new[] { facts.Publish(lease, fact) };
            else
            {
                deliveries = new DomainFactDelivery[2];
                if (!facts.PublishBatch(new[] { DomainFactEmission.Create(lease, fact), companion },
                        deliveries, out string publicationFailure))
                    throw new InvalidOperationException(publicationFailure);
            }
            mayBeCommitted = deliveries.Any(d => d.MayBeCommitted);
            if (!mayBeCommitted)
                throw new InvalidOperationException(string.Join(" | ", deliveries.Select(d => d.Detail)));
            receipt = CreateReceipt(journal, true, false);
            journal.publicationConfirmed = deliveries.All(d => d.Completed);
            repository.UpdatePhysicalItemTransformation(journal);
            receipt = CreateReceipt(journal, !journal.publicationConfirmed, false);
            if (!journal.publicationConfirmed && companion == null) QueueRecovery(operation);
        }
        catch (Exception exception)
        {
            // The dispatcher must represent any ambiguous commit as InDoubt.
            // Once crossed, never roll back effects or replace publication identities.
            if (mayBeCommitted) throw;
            receipt = default;
            facts.Cancel(lease);
            try
            {
                RollbackCommittedTransform(quantitiesBefore, changedPositions, sourceMutations);
                repository.RemovePhysicalItemTransformation(operation);
            }
            catch (Exception rollbackException)
            {
                journal.rollbackBlocked = true;
                journal.rollbackFailure = "Transform rollback failed; manual recovery required.";
                if (repository.TryGetPhysicalItemTransformation(operation, out _))
                    repository.UpdatePhysicalItemTransformation(journal);
                throw new InvalidOperationException(
                    $"Transform '{operation}' failed and its rollback also failed.",
                    new AggregateException(exception, rollbackException));
            }
            return Fail(publicationStarted ? PhysicalItemTransformFailureCode.OutcomeCommitFailed
                    : PhysicalItemTransformFailureCode.OutputCommitFailed,
                $"Transform '{operation}' rolled back: {exception.Message}",
                out failureCode, out failureReason);
        }
        // Outside the rollback scope. The out receipt remains committed even if
        // presentation fails; a composing caller must preserve that durable state.
        List<Exception> presentationFailures = null;
        foreach (Vector2Int position in changedPositions)
        {
            try { markerPresenter.RefreshAt(position); }
            catch (Exception exception)
            { (presentationFailures ??= new List<Exception>()).Add(exception); }
        }
        if (presentationFailures != null)
            throw new AggregateException("Physical transform committed; marker publication failed.", presentationFailures);
        return true;
    }

    public bool HasTransformation(string operationId) =>
        repository.TryGetPhysicalItemTransformation(operationId ?? string.Empty, out _);

    public bool TryRecoverTransformation(string operationId, DomainFactReplay companion,
        out PhysicalItemTransformReceipt receipt, out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!repository.TryGetPhysicalItemTransformation(operationId ?? string.Empty, out var stored))
        { failureReason = "Physical transform journal is unavailable."; return false; }
        var journal = stored.Clone();
        if (journal.rollbackBlocked)
        { failureReason = journal.rollbackFailure; return false; }
        if (!string.Equals(journal.companionPublicationKey,
                companion?.PublicationKey ?? string.Empty, StringComparison.Ordinal))
        { failureReason = "Physical transform recovery requires its exact companion."; return false; }
        receipt = CreateReceipt(journal, !journal.publicationConfirmed, true);
        if (journal.publicationConfirmed) return true;
        var requests = new List<DomainFactReplay>
        {
            DomainFactReplay.Create(journal.publicationKey, new PhysicalItemTransformedFact(journal.fact))
        };
        if (companion != null) requests.Add(companion);
        var results = new DomainFactDelivery[requests.Count];
        bool accepted;
        try { accepted = recovery.ReplayBatch(requests, results, out failureReason); }
        catch (Exception exception)
        {
            // Recovery never repeats or undoes already applied physical effects.
            failureReason = exception.Message;
            return true;
        }
        if (accepted && results.All(result => result.Completed))
        {
            journal.publicationConfirmed = true;
            repository.UpdatePhysicalItemTransformation(journal);
            receipt = CreateReceipt(journal, false, true);
            failureReason = string.Empty;
        }
        else if (string.IsNullOrEmpty(failureReason))
            failureReason = string.Join(" | ", results.Select(result => result.Detail));
        return true;
    }

    public void OnRestoreCompleted()
    {
        pending.Clear();
        queued.Clear();
        LastRecoveryFailure = string.Empty;
        nextRecoveryTime = 0f;
        foreach (string operation in repository.PendingPhysicalTransformationIds()) QueueRecovery(operation);
    }

    public void Tick()
    {
        if (pending.Count == 0 || Time.unscaledTime < nextRecoveryTime) return;
        nextRecoveryTime = Time.unscaledTime + 1f;
        int count = Math.Min(8, pending.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            string operation = pending.Dequeue();
            queued.Remove(operation);
            if (!repository.TryGetPhysicalItemTransformation(operation, out var row)
                || row.publicationConfirmed || row.rollbackBlocked) continue;
            TryRecoverTransformation(operation, null, out var receipt, out string detail);
            if (!receipt.IsCommitted || receipt.PublicationPending)
            { QueueRecovery(operation); failure = detail; }
        }
        if (!string.IsNullOrEmpty(failure) && failure != LastRecoveryFailure)
            Debug.LogWarning("Physical transform fact recovery pending: " + failure);
        LastRecoveryFailure = failure;
    }

    private void QueueRecovery(string operation)
    { if (queued.Add(operation)) pending.Enqueue(operation); }

    private static PhysicalItemTransformReceipt CreateReceipt(
        PhysicalItemTransformationSaveData journal, bool pendingDelivery, bool replayed) =>
        new(journal.fact.operationId, journal.fact.reasonCode,
            journal.fact.inputs.Select(input => input.stackId).ToArray(),
            journal.fact.inputQuantity, journal.fact.inputMassGrams,
            journal.fact.outputMassGrams, journal.fact.outputQuantity, pendingDelivery, replayed);

    private static bool MatchesRequest(PhysicalItemTransformationData fact, string reason,
        bool wholeStack, PhysicalItemTransformInput[] inputs, PhysicalItemTransformOutput[] outputs)
    {
        var ordered = inputs.OrderBy(input => input.StackId, StringComparer.Ordinal).ToArray();
        if (fact.reasonCode != reason || fact.wholeStack != wholeStack
            || fact.inputs.Count != ordered.Length || fact.outputs.Count != outputs.Length) return false;
        for (int index = 0; index < ordered.Length; index++)
            if (fact.inputs[index].stackId != ordered[index].StackId
                || fact.inputs[index].quantity != ordered[index].Quantity) return false;
        for (int index = 0; index < outputs.Length; index++)
        {
            var saved = fact.outputs[index];
            var request = outputs[index];
            if (saved.itemId != request.ItemId || saved.quantity != request.Quantity
                || saved.x != request.Position.x || saved.y != request.Position.y
                || saved.state != (int)request.State || saved.destinationId != request.DestinationId) return false;
        }
        return true;
    }

    private void RollbackCommittedTransform(
        IReadOnlyDictionary<string, int> quantitiesBefore,
        IReadOnlyCollection<Vector2Int> positions,
        IReadOnlyList<SourceMutation> sources)
    {
        try
        {
            RollbackOutputs(quantitiesBefore, positions);
        }
        finally
        {
            RollbackSources(sources);
            foreach (Vector2Int position in positions)
            {
                markerPresenter.RefreshAt(position);
            }
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
