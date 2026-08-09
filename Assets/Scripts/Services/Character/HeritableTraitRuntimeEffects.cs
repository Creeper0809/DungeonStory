using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IHeritableTraitEffectQuery
{
    float GetMultiplier(
        CharacterId characterId,
        HeritableTraitConsequenceKind kind,
        string targetId);
}

public sealed class HeritableTraitEffectQuery : IHeritableTraitEffectQuery
{
    private readonly ICharacterNarrativeQuery narratives;
    private readonly IReadOnlyDictionary<string, HeritableTraitDefinitionSO> definitions;

    public HeritableTraitEffectQuery(
        ICharacterNarrativeQuery narratives,
        IGameContentCatalog content)
    {
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        definitions = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<HeritableTraitDefinitionSO>()
            .Where(value => value != null)
            .ToDictionary(value => value.traitId, StringComparer.Ordinal);
    }

    public float GetMultiplier(
        CharacterId characterId,
        HeritableTraitConsequenceKind kind,
        string targetId)
    {
        if (!characterId.IsValid
            || !narratives.TryGet(characterId, out CharacterNarrativeSnapshot narrative))
            return 1f;
        HeritableTraitDefinitionSO[] expressed = narrative.ExpressedHeritableTraitIds
            .Where(definitions.ContainsKey)
            .Select(value => definitions[value])
            .ToArray();
        return 1f + HeritableTraitModifierResolver.ResolveCappedDelta(
            expressed,
            kind,
            targetId);
    }
}

public readonly struct CharacterTraitReactionEvent
{
    public CharacterTraitReactionEvent(
        IReadOnlyCollection<CharacterId> participantIds,
        params string[] triggerTags)
    {
        ParticipantIds = participantIds ?? Array.Empty<CharacterId>();
        TriggerTags = triggerTags ?? Array.Empty<string>();
    }

    public IReadOnlyCollection<CharacterId> ParticipantIds { get; }
    public IReadOnlyCollection<string> TriggerTags { get; }
}

public interface ICharacterTraitReactionService
{
    int Apply(CharacterActor actor, params string[] triggerTags);
}

public sealed class CharacterTraitReactionRuntime :
    ICharacterTraitReactionService,
    IStartable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterWorldQuery world;
    private readonly List<IDisposable> subscriptions = new();

    public CharacterTraitReactionRuntime(
        IGameEventBus events,
        ICharacterWorldQuery world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Start()
    {
        subscriptions.Add(events.Subscribe<CharacterTraitReactionEvent>(
            OnTraitReaction));
        subscriptions.Add(events.Subscribe<PhysicalMealConsumedEvent>(
            OnMealConsumed));
        subscriptions.Add(events.Subscribe<BlueprintResearchCompletedEvent>(
            _ => ApplyToAll("event:research-completed")));
        subscriptions.Add(events.Subscribe<FestivalCelebratedEvent>(
            OnFestivalCelebrated));
        subscriptions.Add(events.Subscribe<InvasionResolvedEvent>(
            invasion => ApplyToAll(
                invasion.defended
                    ? "event:combat-victory"
                    : "danger:exposed")));
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in subscriptions)
            subscription?.Dispose();
        subscriptions.Clear();
    }

    public int Apply(CharacterActor actor, params string[] triggerTags)
    {
        if (actor?.Identity?.Data == null)
            return 0;
        HashSet<string> tags = (triggerTags ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.Ordinal);
        if (tags.Count == 0)
            return 0;

        int applied = 0;
        foreach (CharacterTraitSO trait in actor.Identity.Data.traits
                     ?? Array.Empty<CharacterTraitSO>())
        {
            foreach (CharacterTraitMoodReaction reaction in trait?.moodReactions
                         ?? new List<CharacterTraitMoodReaction>())
            {
                if (reaction == null
                    || !reaction.IsValid
                    || !tags.Contains(reaction.triggerTag.Trim()))
                    continue;
                actor.ApplyMoodFactor(
                    $"trait:{trait.id}:{reaction.triggerTag.Trim()}",
                    $"{trait.traitName}: {reaction.triggerTag.Trim()}",
                    reaction.moodDelta,
                    Mathf.Max(1, reaction.durationDays)
                        * GameCalendarRules.SecondsPerDay,
                    1);
                applied++;
            }
        }
        return applied;
    }

    private void OnTraitReaction(CharacterTraitReactionEvent eventType)
    {
        HashSet<CharacterId> participants = eventType.ParticipantIds
            .Where(value => value.IsValid)
            .ToHashSet();
        foreach (CharacterActor actor in world.Characters)
        {
            if (CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                && participants.Contains(id))
                Apply(actor, eventType.TriggerTags.ToArray());
        }
    }

    private void OnMealConsumed(PhysicalMealConsumedEvent eventType)
    {
        if (!eventType.Result.Success)
            return;
        Apply(
            eventType.Actor,
            "food:sated",
            eventType.Result.Contaminated
                ? "food:contaminated"
                : "food:safe-meal");
    }

    private void OnFestivalCelebrated(FestivalCelebratedEvent eventType)
    {
        events.Publish(new CharacterTraitReactionEvent(
            eventType.ParticipantIds,
            "festival:prepared",
            "event:minor-success",
            "culture:harmony",
            "event:audience",
            eventType.FestivalId));
    }

    private void ApplyToAll(params string[] tags)
    {
        foreach (CharacterActor actor in world.Characters)
            Apply(actor, tags);
    }
}
