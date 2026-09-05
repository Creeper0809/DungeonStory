using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
{
    Applied = 1,
    AlreadyApplied = 2,
    Deferred = 3,
    Corruption = 4
}

public readonly struct CharacterMedicalSupplyDestinationDrainCheckpointGcResult
{
    public CharacterMedicalSupplyDestinationDrainCheckpointGcResult(
        CharacterMedicalSupplyDestinationDrainCheckpointGcStatus status,
        string message)
    {
        Status = status;
        Message = message ?? string.Empty;
    }

    public CharacterMedicalSupplyDestinationDrainCheckpointGcStatus Status
        { get; }
    public string Message { get; }
}

public interface ICharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator
{
    CharacterMedicalSupplyDestinationDrainCheckpointGcResult
        OnDurableSaveCommitted(string slotId, string serializedByteDigest);
}

internal interface
    ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate
{
    IReadOnlyList<CharacterMedicalSupplyDestinationDrainJoinData>
        ClosedJoins { get; }
}

internal interface
    ICharacterMedicalSupplyDestinationDrainCheckpointGcAuthority
{
    bool TryPrepare(
        out ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate
            candidate,
        out string failureReason);

    bool TryPublish(
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate candidate,
        out string failureReason);

    void Rollback(
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate candidate);

    void Complete(
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate candidate);
}

/// <summary>
/// Removes only Character Medical destination-lifetime joins that were already
/// captured by a durable save. The live aggregate and exact join objects remain
/// authoritative until child-first publication succeeds.
/// </summary>
internal sealed class
    CharacterMedicalSupplyDestinationDrainCheckpointGcAuthority :
    ICharacterMedicalSupplyDestinationDrainCheckpointGcAuthority
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private Candidate active;

    internal CharacterMedicalSupplyDestinationDrainCheckpointGcAuthority(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public bool TryPrepare(
        out ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate
            candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (active != null)
        {
            failureReason =
                "character-medical-supply-drain-checkpoint-gc-already-active";
            return false;
        }
        if (aggregateRootStore.IsRestoreStaging)
        {
            failureReason =
                "character-medical-supply-drain-checkpoint-gc-restore-active";
            return false;
        }

        CharacterMedicalAggregateState state = aggregateRootStore.GetOrCreate(
            () => new CharacterMedicalAggregateState());
        List<OrderEntry> entries = new();
        foreach (CharacterMedicalOrder order in state.Orders
                     .Where(value => value != null)
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            CharacterMedicalSupplyDestinationDrainJoinData[] closed =
                (order.treatmentDestinationDrainJoins
                     ?? new List<
                         CharacterMedicalSupplyDestinationDrainJoinData>())
                .Where(value => value != null
                    && value.phase ==
                        CharacterMedicalSupplyDestinationDrainPhase
                            .ClosedAwaitingCheckpointGc)
                .OrderBy(value => value.destinationSequence)
                .ToArray();
            if (closed.Length == 0)
                continue;

            if (closed.Any(value =>
                    !CharacterMedicalSupplyDestinationDrainValidation
                        .TryValidateJoin(order, value, out _)))
            {
                failureReason =
                    "character-medical-supply-drain-checkpoint-gc-upper-invalid:"
                    + order.orderId;
                return false;
            }

            entries.Add(new OrderEntry(
                order,
                CharacterMedicalOrderPersistence.Clone(order),
                closed));
        }

        active = new Candidate(entries);
        candidate = active;
        return true;
    }

    public bool TryPublish(
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequire(candidate, out Candidate exact, out failureReason))
            return false;
        if (exact.Published)
            return true;
        if (aggregateRootStore.IsRestoreStaging)
        {
            failureReason =
                "character-medical-supply-drain-checkpoint-gc-restore-active";
            return false;
        }

        CharacterMedicalAggregateState state = aggregateRootStore.GetOrCreate(
            () => new CharacterMedicalAggregateState());
        foreach (OrderEntry entry in exact.Entries)
        {
            if (!state.Orders.Contains(entry.LiveOrder)
                || !OrderExact(entry.LiveOrder, entry.OriginalOrder)
                || entry.LiveClosedJoins.Any(value =>
                    !entry.LiveOrder.treatmentDestinationDrainJoins.Contains(
                        value)))
            {
                failureReason =
                    "character-medical-supply-drain-checkpoint-gc-upper-drift:"
                    + entry.OriginalOrder.orderId;
                return false;
            }
        }

        foreach (OrderEntry entry in exact.Entries)
        {
            foreach (CharacterMedicalSupplyDestinationDrainJoinData join in
                     entry.LiveClosedJoins)
            {
                entry.LiveOrder.treatmentDestinationDrainJoins.Remove(join);
            }
            SortJoins(entry.LiveOrder);
        }
        exact.Published = true;
        return true;
    }

    public void Rollback(
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate candidate)
    {
        if (!TryRequire(candidate, out Candidate exact, out string failureReason))
            throw new InvalidOperationException(failureReason);
        if (!exact.Published)
        {
            exact.Completed = true;
            active = null;
            return;
        }

        CharacterMedicalAggregateState state = aggregateRootStore.GetOrCreate(
            () => new CharacterMedicalAggregateState());
        foreach (OrderEntry entry in exact.Entries)
        {
            CharacterMedicalOrder expectedCleared =
                CharacterMedicalOrderPersistence.Clone(entry.OriginalOrder);
            HashSet<int> removedSequences = entry.LiveClosedJoins
                .Select(value => value.destinationSequence)
                .ToHashSet();
            expectedCleared.treatmentDestinationDrainJoins.RemoveAll(value =>
                value != null
                && removedSequences.Contains(value.destinationSequence));
            SortJoins(expectedCleared);
            if (!state.Orders.Contains(entry.LiveOrder)
                || !OrderExact(entry.LiveOrder, expectedCleared))
            {
                throw new InvalidOperationException(
                    "character-medical-supply-drain-checkpoint-gc-rollback-drift:"
                    + entry.OriginalOrder.orderId);
            }
        }

        foreach (OrderEntry entry in exact.Entries)
        {
            entry.LiveOrder.treatmentDestinationDrainJoins.AddRange(
                entry.LiveClosedJoins);
            SortJoins(entry.LiveOrder);
            if (!OrderExact(entry.LiveOrder, entry.OriginalOrder))
            {
                throw new InvalidOperationException(
                    "character-medical-supply-drain-checkpoint-gc-rollback-invalid:"
                    + entry.OriginalOrder.orderId);
            }
        }
        exact.Published = false;
        exact.Completed = true;
        active = null;
    }

    public void Complete(
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate candidate)
    {
        if (!TryRequire(candidate, out Candidate exact, out string failureReason))
            throw new InvalidOperationException(failureReason);
        if (!exact.Published)
        {
            throw new InvalidOperationException(
                "character-medical-supply-drain-checkpoint-gc-not-published");
        }
        exact.Completed = true;
        active = null;
    }

    private bool TryRequire(
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate candidate,
        out Candidate exact,
        out string failureReason)
    {
        exact = candidate as Candidate;
        if (exact == null || !ReferenceEquals(exact, active) || exact.Completed)
        {
            failureReason =
                "character-medical-supply-drain-checkpoint-gc-candidate-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool OrderExact(
        CharacterMedicalOrder left,
        CharacterMedicalOrder right) =>
        left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static void SortJoins(CharacterMedicalOrder order)
    {
        order.treatmentDestinationDrainJoins ??=
            new List<CharacterMedicalSupplyDestinationDrainJoinData>();
        order.treatmentDestinationDrainJoins.Sort((left, right) =>
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.destinationSequence.CompareTo(right.destinationSequence);
        });
    }

    private sealed class OrderEntry
    {
        internal OrderEntry(
            CharacterMedicalOrder liveOrder,
            CharacterMedicalOrder originalOrder,
            IReadOnlyList<CharacterMedicalSupplyDestinationDrainJoinData>
                liveClosedJoins)
        {
            LiveOrder = liveOrder
                ?? throw new ArgumentNullException(nameof(liveOrder));
            OriginalOrder = originalOrder
                ?? throw new ArgumentNullException(nameof(originalOrder));
            LiveClosedJoins = liveClosedJoins
                ?? throw new ArgumentNullException(nameof(liveClosedJoins));
        }

        internal CharacterMedicalOrder LiveOrder { get; }
        internal CharacterMedicalOrder OriginalOrder { get; }
        internal IReadOnlyList<CharacterMedicalSupplyDestinationDrainJoinData>
            LiveClosedJoins { get; }
    }

    private sealed class Candidate :
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate
    {
        internal Candidate(IReadOnlyList<OrderEntry> entries)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            ClosedJoins = entries
                .SelectMany(value => value.LiveClosedJoins)
                .ToArray();
        }

        internal IReadOnlyList<OrderEntry> Entries { get; }
        public IReadOnlyList<CharacterMedicalSupplyDestinationDrainJoinData>
            ClosedJoins { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }
}

public sealed class CharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator :
    ICharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICharacterMedicalSupplyDestinationDrainCheckpointGcAuthority
        upper;
    private readonly IFacilityBufferDestinationCustodyDrainLiveQuery childQuery;
    private readonly IFacilityBufferDestinationCustodyDrainCheckpointGcPort
        childGc;

    [VContainer.Inject]
    public CharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IFacilityBufferDestinationCustodyDrainLiveQuery childQuery,
        IFacilityBufferDestinationCustodyDrainCheckpointGcPort childGc)
        : this(
            aggregateRootStore,
            new CharacterMedicalSupplyDestinationDrainCheckpointGcAuthority(
                aggregateRootStore),
            childQuery,
            childGc)
    {
    }

    internal CharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterMedicalSupplyDestinationDrainCheckpointGcAuthority upper,
        IFacilityBufferDestinationCustodyDrainLiveQuery childQuery,
        IFacilityBufferDestinationCustodyDrainCheckpointGcPort childGc)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.upper = upper ?? throw new ArgumentNullException(nameof(upper));
        this.childQuery = childQuery
            ?? throw new ArgumentNullException(nameof(childQuery));
        this.childGc = childGc ?? throw new ArgumentNullException(nameof(childGc));
    }

    public CharacterMedicalSupplyDestinationDrainCheckpointGcResult
        OnDurableSaveCommitted(string slotId, string serializedByteDigest)
    {
        if (string.IsNullOrEmpty(slotId)
            || serializedByteDigest == null
            || serializedByteDigest.Length != 64)
        {
            return Corruption(
                "character-medical-supply-drain-checkpoint-gc-context-invalid");
        }

        if (!upper.TryPrepare(
                out ICharacterMedicalSupplyDestinationDrainCheckpointGcCandidate
                    upperCandidate,
                out string failureReason))
        {
            return Deferred(failureReason);
        }

        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
            childCandidate = null;
        bool childPrepared = false;
        bool childPublished = false;
        bool upperPublished = false;
        try
        {
            CharacterMedicalAggregateState state = aggregateRootStore.GetOrCreate(
                () => new CharacterMedicalAggregateState());
            FacilityBufferDestinationCustodyDrainSnapshot[] allChildren =
                childQuery.Drains?.ToArray()
                ?? Array.Empty<FacilityBufferDestinationCustodyDrainSnapshot>();
            CharacterMedicalSupplyDestinationDrainCrossAggregateJoin.Validate(
                state.Orders,
                allChildren);

            Dictionary<string, FacilityBufferDestinationCustodyDrainSnapshot>
                childByStep = allChildren.ToDictionary(
                    value => value.StepOperationId,
                    StringComparer.Ordinal);
            FacilityBufferDestinationCustodyDrainSnapshot[] closedChildren =
                upperCandidate.ClosedJoins.Select(join =>
                {
                    if (!childByStep.TryGetValue(
                            join.stepOperationId,
                            out FacilityBufferDestinationCustodyDrainSnapshot child)
                        || child.Phase !=
                            FacilityBufferDestinationCustodyDrainPhase
                                .OwnerAcknowledgedAwaitingCheckpointGc)
                    {
                        throw new InvalidOperationException(
                            "character-medical-supply-drain-checkpoint-gc-child-invalid:"
                            + join.stepOperationId);
                    }
                    return child;
                }).ToArray();

            if (closedChildren.Length == 0)
            {
                if (!upper.TryPublish(upperCandidate, out failureReason))
                {
                    upper.Rollback(upperCandidate);
                    return Corruption(failureReason);
                }
                upperPublished = true;
                upper.Complete(upperCandidate);
                return new CharacterMedicalSupplyDestinationDrainCheckpointGcResult(
                    CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                        .AlreadyApplied,
                    "No Character Medical supply destination receipts require collection.");
            }

            if (!childGc.TryPrepareCheckpointGarbageCollection(
                    closedChildren,
                    out childCandidate,
                    out failureReason))
            {
                upper.Rollback(upperCandidate);
                return Deferred(failureReason);
            }
            childPrepared = true;
            if (!childGc.TryPublishCheckpointGarbageCollection(
                    childCandidate,
                    out failureReason))
            {
                childGc.RollbackCheckpointGarbageCollection(childCandidate);
                childGc.CompleteCheckpointGarbageCollection(childCandidate);
                upper.Rollback(upperCandidate);
                return Corruption(failureReason);
            }
            childPublished = true;
            if (!upper.TryPublish(upperCandidate, out failureReason))
            {
                childGc.RollbackCheckpointGarbageCollection(childCandidate);
                childGc.CompleteCheckpointGarbageCollection(childCandidate);
                childPublished = false;
                upper.Rollback(upperCandidate);
                return Corruption(failureReason);
            }
            upperPublished = true;

            upper.Complete(upperCandidate);
            childGc.CompleteCheckpointGarbageCollection(childCandidate);
            return new CharacterMedicalSupplyDestinationDrainCheckpointGcResult(
                CharacterMedicalSupplyDestinationDrainCheckpointGcStatus.Applied,
                $"Collected {closedChildren.Length} Character Medical supply destination receipt(s)." );
        }
        catch (Exception exception)
        {
            try
            {
                bool upperHandled = false;
                if (upperPublished)
                {
                    upper.Rollback(upperCandidate);
                    upperHandled = true;
                }
                if (childPrepared && childCandidate != null)
                {
                    if (childPublished)
                    {
                        childGc.RollbackCheckpointGarbageCollection(
                            childCandidate);
                    }
                    childGc.CompleteCheckpointGarbageCollection(childCandidate);
                }
                if (!upperHandled)
                {
                    // Rollback also completes a never-published candidate.
                    upper.Rollback(upperCandidate);
                }
            }
            catch (Exception rollbackException)
            {
                return Corruption(
                    exception.Message + "; rollback=" + rollbackException.Message);
            }
            return Corruption(exception.Message);
        }
    }

    private static CharacterMedicalSupplyDestinationDrainCheckpointGcResult
        Deferred(string message) => new(
        CharacterMedicalSupplyDestinationDrainCheckpointGcStatus.Deferred,
        message);

    private static CharacterMedicalSupplyDestinationDrainCheckpointGcResult
        Corruption(string message) => new(
        CharacterMedicalSupplyDestinationDrainCheckpointGcStatus.Corruption,
        message);
}
