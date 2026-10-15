using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Records post-commit narrative experience evidence from frozen node facts.
/// Character experience itself is owned and applied before this observation.
/// </summary>
public sealed class OffenseNodeExperienceObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterLifetimeQuery characters;
    private IDisposable subscription;

    public OffenseNodeExperienceObserver(
        IGameEventBus events,
        ICharacterLifetimeQuery characters)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
    }

    public void Initialize()
    {
        if (subscription != null)
            return;
        subscription = events.Subscribe<OffenseNodeExperienceObservedFact>(
            Observe);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(OffenseNodeExperienceObservedFact fact)
    {
        OffenseNodeExperienceObservedFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        OffenseNodeResolutionFactData resolution =
            value.Resolution.Snapshot();
        OffenseNodeExperienceData experience = value.Experience;
        CharacterActor[] matches = (characters.AllCharacters
                ?? Array.Empty<CharacterActor>())
            .Where(candidate => candidate != null
                && string.Equals(
                    candidate.Identity?.PersistentId,
                    experience.characterId,
                    StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                matches.Length == 0
                    ? "offense-node-experience-character-unavailable:"
                        + experience.characterId
                    : "offense-node-experience-character-ambiguous:"
                        + experience.characterId);
        }

        CharacterActor actor = matches[0];
        if (actor.Progression == null)
        {
            throw new InvalidOperationException(
                "offense-node-experience-progression-unavailable:"
                + experience.characterId);
        }

        actor.Progression.RecordNarrative(
            CharacterNarrativeDomain.Expedition,
            "node:" + resolution.nodeKind,
            resolution.targetId,
            "resolved",
            experience.requestedAmount,
            resolution.absoluteDay,
            eventContext: new GameplayNarrativeEventContext
            {
                eventInstanceId = resolution.expeditionId
                    + ":node:" + resolution.nodeId
                    + ":" + experience.characterId,
                chainId = resolution.expeditionId,
                locationId = resolution.targetId,
                locationDisplayName = !string.IsNullOrWhiteSpace(
                    resolution.regionDisplayName)
                    ? resolution.regionDisplayName
                    : resolution.targetTitle,
                actorId = experience.characterId,
                actorDisplayName = experience.displayName,
                counterpartyId = resolution.factionId,
                counterpartyDisplayName = resolution.targetTitle,
                usedObjectId = resolution.nodeId,
                usedObjectDisplayName = resolution.nodeTitle,
                resultDetail = string.IsNullOrWhiteSpace(
                    resolution.nodeDescription)
                    ? resolution.nodeKind.ToString()
                    : resolution.nodeDescription
            });
    }
}
