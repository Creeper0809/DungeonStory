using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Records after-effects battle narrative evidence for the exact surviving actor.
/// Battle effects and experience are applied before this observation.
/// </summary>
public sealed class OffenseBattleExperienceObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterLifetimeQuery characters;
    private IDisposable subscription;

    public OffenseBattleExperienceObserver(
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
        subscription = events.Subscribe<OffenseBattleExperienceFact>(Observe);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    internal static GameplayNarrativeEventContext CreateContext(
        OffenseBattleExperienceFact fact)
    {
        OffenseBattleExperienceFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        return new GameplayNarrativeEventContext
        {
            eventInstanceId = value.BattleId + ":result:" + value.CharacterId,
            chainId = value.ExpeditionId,
            locationId = value.TargetId,
            locationDisplayName = !string.IsNullOrWhiteSpace(value.RegionName)
                ? value.RegionName
                : value.TargetTitle,
            actorId = value.CharacterId,
            actorDisplayName = value.CharacterName,
            counterpartyId = value.FactionId,
            counterpartyDisplayName = value.TargetTitle,
            usedObjectId = value.NodeId,
            usedObjectDisplayName = value.NodeTitle,
            resultDetail = value.Victory ? "교전 승리" : "교전 생존"
        };
    }

    private void Observe(OffenseBattleExperienceFact fact)
    {
        OffenseBattleExperienceFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        CharacterActor[] matches = (characters.AllCharacters
                ?? Array.Empty<CharacterActor>())
            .Where(candidate => candidate != null
                && string.Equals(
                    candidate.Identity?.PersistentId,
                    value.CharacterId,
                    StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                matches.Length == 0
                    ? "offense-battle-experience-character-unavailable:"
                        + value.CharacterId
                    : "offense-battle-experience-character-ambiguous:"
                        + value.CharacterId);
        }

        CharacterActor actor = matches[0];
        if (actor.Progression == null)
        {
            throw new InvalidOperationException(
                "offense-battle-experience-progression-unavailable:"
                + value.CharacterId);
        }

        actor.Progression.RecordNarrative(
            CharacterNarrativeDomain.Combat,
            value.Victory ? "battle-victory" : "battle-survived",
            value.TargetId,
            value.Victory ? "won" : "survived",
            value.DamageTaken,
            triggerPassives: false,
            eventContext: CreateContext(value));
    }
}
