using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Owns shared operation IDs, terminal receipts, and their fact publication/recovery.</summary>
internal sealed class CharacterConsumablesOperationRuntime
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IGameClock clock;
    private readonly IDomainFactPublisher domainFacts;
    private readonly IGameCalendar calendar;

    internal CharacterConsumablesOperationRuntime(DungeonRuntimeAggregateRootStore aggregateRootStore,
        IGameClock clock, IDomainFactPublisher domainFacts, IGameCalendar calendar)
    {
        this.aggregateRootStore = aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.domainFacts = domainFacts;
        this.calendar = calendar;
    }

    private CharacterConsumablesAggregateState ReadState =>
        aggregateRootStore.GetOrCreate(() => new CharacterConsumablesAggregateState());
    private CharacterConsumablesAggregateState WriteState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterConsumablesAggregateState(), state => state.Clone());
    private float TimelineNow => ReadState.GetTimelineTime(clock.Time);

    internal bool TryGetConsumptionFactDay(out int day)
    {
        day = 0;
        if (domainFacts == null || calendar == null || calendar.Day < 0)
        {
            Debug.LogError("character-consumables-fact-context-unavailable");
            return false;
        }
        day = Math.Max(1, calendar.Day);
        return true;
    }

    internal bool TryReserveConsumptionFact<TFact>(in TFact fact, out DomainFactLease lease)
    {
        lease = default;
        string failure = "publisher-unavailable";
        if (domainFacts != null && domainFacts.TryReserve(fact, out lease, out failure))
        {
            if (!string.IsNullOrWhiteSpace(lease.PublicationKey)) return true;
            domainFacts.Cancel(lease);
            failure = "reserved-publication-key-missing";
        }
        Debug.LogError("character-consumables-fact-reservation-failed:" + failure);
        return false;
    }

    internal bool PublishConsumptionFacts(DomainFactEmission[] emissions,
        CharacterConsumablesAggregateState rollback)
    {
        string[] keys = emissions.Select(value => value.Lease.PublicationKey).ToArray();
        var deliveries = new DomainFactDelivery[emissions.Length];
        bool accepted = domainFacts.PublishBatch(emissions, deliveries, out string failure);
        if (!accepted && deliveries.All(value => !value.MayBeCommitted))
        {
            // Physical consumption/effects before this local snapshot remain;
            // only the staged completion (and detox effect) can be rolled back.
            aggregateRootStore.Replace(rollback);
            Debug.LogError("character-consumables-fact-publication-rejected:" + failure);
            return false;
        }
        return ConsumptionFactsDurable(keys, deliveries, failure);
    }

    internal bool ReplayConsumptionFacts(DomainFactReplay[] facts)
    {
        if (domainFacts is not IDomainFactRecoveryPublisher recovery)
        {
            Debug.LogError("character-consumables-fact-recovery-unavailable");
            return false;
        }
        var deliveries = new DomainFactDelivery[facts.Length];
        recovery.ReplayBatch(facts, deliveries, out string failure);
        // An earlier attempt may have committed. A rejected recovery never
        // authorizes rolling back physical/effect state from that attempt.
        return ConsumptionFactsDurable(facts.Select(value => value.PublicationKey).ToArray(),
            deliveries, failure);
    }

    private static bool ConsumptionFactsDurable(string[] keys, DomainFactDelivery[] deliveries,
        string failure)
    {
        for (int i = 0; i < deliveries.Length; i++)
        {
            var delivery = deliveries[i];
            if (delivery.Phase is not (DomainFactDeliveryPhase.Delivered
                    or DomainFactDeliveryPhase.AcceptedPendingDelivery)
                || string.IsNullOrWhiteSpace(keys[i]) || delivery.OperationKey != keys[i])
            {
                Debug.LogError("character-consumables-fact-publication-pending:"
                    + delivery.Phase + ":" + delivery.Detail + ":" + failure);
                return false;
            }
        }
        return true;
    }

    internal static void PublishPostCommitObserver(
        Action observer,
        string context)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                (context ?? "character-consumables-post-commit-observer")
                + ":"
                + exception.GetType().Name);
        }
    }

    internal void RecordCompletedOperation(
        ConsumableOperationId operationId,
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        ItemStackId stackId,
        bool meal,
        bool policyViolation = false,
        bool contaminated = false,
        bool detox = false,
        BuildingInstanceId facilityId = default,
        float appliedEffect = 0f)
    {
        WriteState.CompletedOperations.Add(operationId, new CharacterConsumableOperationState
        {
            operationId = operationId.Value,
            characterId = characterId.Value,
            itemDefinitionId = itemId.Value,
            itemStackId = stackId.Value,
            facilityInstanceId = facilityId.Value,
            meal = meal,
            detox = detox,
            policyViolation = policyViolation,
            contaminated = contaminated,
            appliedEffect = appliedEffect,
            completedAt = TimelineNow
        });
    }

    internal static bool IsAllowedOperationId(
        ConsumableOperationId operationId,
        bool automaticOperation) =>
        automaticOperation
            ? CharacterConsumableIdContract.IsCurrentAutomaticOperation(
                operationId.Value)
            : CharacterConsumableIdContract.IsExternalOperation(operationId.Value);

    internal ConsumableOperationId NewOperationId()
    {
        CharacterConsumablesAggregateState state = WriteState;
        long sequence = state.NextOperationSequence;
        if (sequence < 1L || sequence == long.MaxValue)
        {
            throw new InvalidOperationException(
                "Character consumables operation sequence is exhausted.");
        }
        state.NextOperationSequence = sequence + 1L;
        return CharacterConsumableIdContract.CreateAutomaticOperation(sequence);
    }

    internal void CancelFact(in DomainFactLease lease) => domainFacts.Cancel(lease);

    internal void SetOutcomeResultKey(ConsumableOperationId operationId, string key) =>
        WriteState.CompletedOperations[operationId].outcomeResultKey = key;

    internal void SetMealEffectsSkippedReason(ConsumableOperationId operationId, string reason) =>
        WriteState.CompletedOperations[operationId].mealEffectsSkippedReason = reason;

    internal void ReconcilePersistentActors(IReadOnlyCollection<CharacterId> persistent,
        IReadOnlyCollection<ConsumableOperationId> retainedMealOperations)
    {
        ConsumableOperationId[] orphaned = ReadState.CompletedOperations
            .Where(pair => pair.Value == null || !persistent.Contains(pair.Value.CharacterId)
                && !retainedMealOperations.Contains(pair.Key))
            .Select(pair => pair.Key).ToArray();
        if (orphaned.Length == 0) return;
        CharacterConsumablesAggregateState state = WriteState;
        foreach (ConsumableOperationId id in orphaned) state.CompletedOperations.Remove(id);
    }
}
