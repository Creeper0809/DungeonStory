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
        IEnumerable<CharacterId> witnessIds)
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

        CharacterId = characterId;
        Cause = cause;
        AbsoluteDay = absoluteDay;
        Location = location;
        WitnessIds = (witnessIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid && !value.Equals(characterId))
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public CharacterId CharacterId { get; }
    public CharacterDeathCauseCode Cause { get; }
    public int AbsoluteDay { get; }
    public CoreGridCell Location { get; }
    public IReadOnlyList<CharacterId> WitnessIds { get; }

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
        CharacterDeathCauseCode cause)
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
            witnesses);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);
}
