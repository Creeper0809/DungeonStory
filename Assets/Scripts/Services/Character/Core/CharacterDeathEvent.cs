using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct CharacterDeathEvent
{
    public CharacterDeathEvent(
        CharacterId characterId,
        CharacterDeathCauseCode cause,
        int absoluteDay,
        CoreGridCell location,
        IEnumerable<CharacterId> witnessIds,
        string parentPublicationKey = "")
    {
        if (!characterId.IsValid)
        {
            throw new ArgumentException(
                "A death event requires a valid character ID.",
                nameof(characterId));
        }

        if (absoluteDay < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteDay),
                "A death event requires a positive absolute day.");
        }
        if (!Enum.IsDefined(typeof(CharacterDeathCauseCode), cause))
            throw new ArgumentOutOfRangeException(nameof(cause));

        CharacterId = characterId;
        Cause = cause;
        AbsoluteDay = absoluteDay;
        Location = location;
        WitnessIds = Array.AsReadOnly((witnessIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid && !value.Equals(characterId))
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray());
        if (parentPublicationKey == null || parentPublicationKey.Length > 0
            && string.IsNullOrWhiteSpace(parentPublicationKey))
            throw new ArgumentException("A death publication reference cannot be null or whitespace.");
        ParentPublicationKey = parentPublicationKey;
    }

    public CharacterId CharacterId { get; }
    public CharacterDeathCauseCode Cause { get; }
    public int AbsoluteDay { get; }
    public CoreGridCell Location { get; }
    public IReadOnlyList<CharacterId> WitnessIds { get; }
    // Opaque correlation only. The observer resolves its canonical record, not this event.
    public string ParentPublicationKey { get; }

    public CharacterLifeDeathRecord ToLifeRecord() => new(
        CharacterId,
        Cause,
        AbsoluteDay,
        Location,
        WitnessIds);
}

public interface ICharacterDeathEventFactory
{
    CharacterDeathEvent Create(
        CharacterActor actor,
        CharacterDeathCauseCode cause);
    CharacterDeathEvent Create(
        CharacterActor actor,
        CharacterDeathCauseCode cause,
        string parentPublicationKey);
}

public sealed class CharacterDeathEventFactory : ICharacterDeathEventFactory
{
    internal const int WitnessRadiusCells = 8;

    private readonly ICharacterWorldQuery world;
    private readonly IGameCalendar calendar;

    public CharacterDeathEventFactory(
        ICharacterWorldQuery world,
        IGameCalendar calendar)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public CharacterDeathEvent Create(
        CharacterActor actor,
        CharacterDeathCauseCode cause) => Create(
        actor,
        cause,
        string.Empty);

    public CharacterDeathEvent Create(
        CharacterActor actor,
        CharacterDeathCauseCode cause,
        string parentPublicationKey)
    {
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        Vector2Int position = actor.GetNowXY();
        CharacterId[] witnesses = world.Characters
            .Where(candidate => candidate != null
                && candidate != actor
                && !candidate.IsDead
                && Manhattan(position, candidate.GetNowXY()) <= WitnessRadiusCells)
            .Select(CharacterPersistentIdentity.Require)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        return new CharacterDeathEvent(
            characterId,
            cause,
            calendar.Day,
            new CoreGridCell(position.x, position.y),
            witnesses,
            parentPublicationKey);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);
}
