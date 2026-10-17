using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20CampaignSaveCodec;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class V20SocietyEventStateStore
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly V20EventSaveValidation eventSaveValidation;
    private bool capturingIncident;

    internal V20SocietyEventStateStore(
        DungeonRuntimeAggregateRootStore rootStore,
        V20EventSaveValidation eventSaveValidation)
    {
        this.rootStore = rootStore;
        this.eventSaveValidation = eventSaveValidation;
    }
    internal SocietyEventAggregateState Current => rootStore.GetOrCreate(() => new SocietyEventAggregateState());
    internal SocietyEventAggregateState Writable
    {
        get
        {
            AssertIncidentIdle();
            return rootStore.GetOrCreateWritable(() => new SocietyEventAggregateState(),
                value => new SocietyEventAggregateState { Data = Clone(value.Data) });
        }
    }
    internal SocietyEventWorldSaveData CaptureSociety()
    { AssertIncidentIdle(); return Clone(Current.Data); }
    internal SocietyEventAggregateState PrepareSociety(SocietyEventWorldSaveData data) => new() { Data = eventSaveValidation.ValidateSociety(Clone(data)) };
    internal void PublishSociety(SocietyEventAggregateState state)
    { AssertIncidentIdle(); rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state))); }

    internal void AssertIncidentIdle()
    {
        if (capturingIncident) throw new InvalidOperationException("observed-service-incident-publication-active");
    }

    internal void AddResolvedSociety(
        SocietyEventWorldSaveData state,
        V20ActiveEventSaveData value)
    {
        state.recentResolvedEvents.Add(value);
        HashSet<string> receiptOwnedOccurrences =
            (state.successfulLifeEventOperations
                ?? new List<ObservedLifeEventReceiptSaveData>())
            .Where(receipt => receipt != null)
            .Select(receipt => receipt.occurrenceInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        int excess = state.recentResolvedEvents.Count(occurrence =>
            occurrence != null
            && !receiptOwnedOccurrences.Contains(occurrence.instanceId)) - 256;
        while (excess-- > 0)
        {
            int removableIndex = state.recentResolvedEvents.FindIndex(occurrence =>
                occurrence != null
                && !receiptOwnedOccurrences.Contains(occurrence.instanceId));
            if (removableIndex < 0) break;
            state.recentResolvedEvents.RemoveAt(removableIndex);
        }
    }

    internal void MarkEventRecurrence(
        SocietyEventWorldSaveData state,
        V20AuthoredContentSO definition,
        V20ActiveEventSaveData resolvedEvent,
        int absoluteDay)
    {
        int cooldown = definition is LifeEventDefinitionSO lifeDefinition
            ? lifeDefinition.cooldownDays
            : 30;
        state.cooldowns.RemoveAll(value => string.Equals(
            value.definitionId,
            definition.StableId,
            StringComparison.Ordinal));
        state.cooldowns.Add(new V20EventCooldownSaveData
        {
            definitionId = definition.StableId,
            availableAbsoluteDay = absoluteDay + Math.Max(3, cooldown)
        });
        string categoryKey = CategoryKey(definition);
        state.cooldowns.RemoveAll(value => string.Equals(
            value.definitionId,
            categoryKey,
            StringComparison.Ordinal));
        state.cooldowns.Add(new V20EventCooldownSaveData
        {
            definitionId = categoryKey,
            availableAbsoluteDay = absoluteDay + 3
        });
        if (definition is not LifeEventDefinitionSO lifeEvent) return;
        switch (lifeEvent.frequencyRule)
        {
            case LifeEventFrequencyRule.OncePerRun:
                if (!state.completedOnceEventIds.Contains(
                        lifeEvent.StableId,
                        StringComparer.Ordinal))
                    state.completedOnceEventIds.Add(lifeEvent.StableId);
                break;
            case LifeEventFrequencyRule.OncePerGeneration:
                AddUnique(
                    state.recurrenceKeys,
                    GenerationRecurrenceKey(
                        lifeEvent.StableId,
                        resolvedEvent?.generation ?? 0));
                break;
            case LifeEventFrequencyRule.OncePerCharacter:
                foreach (string participant in
                         resolvedEvent?.participantCharacterIds
                         ?? new List<string>())
                    AddUnique(
                        state.recurrenceKeys,
                        CharacterRecurrenceKey(lifeEvent.StableId, participant));
                break;
        }
    }

    private static void AddUnique(ICollection<string> target, string value)
    {
        if (!target.Contains(value, StringComparer.Ordinal)) target.Add(value);
    }

    internal void BeginObservedPublication() => capturingIncident = true;

    internal void EndObservedPublication() => capturingIncident = false;

    // The publication transaction alone may replace or roll back its candidate while fenced.
    internal void ReplaceObservedCandidate(SocietyEventAggregateState candidate) => rootStore.Replace(candidate);
}
