using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterActorCollection
{
    public static IReadOnlyList<CharacterActor> DistinctByGameObject(
        IEnumerable<CharacterActor> actors)
    {
        return actors?
            .Where(actor => actor != null)
            .GroupBy(actor => actor.gameObject)
            .Select(group => group.FirstOrDefault(actor => actor.GetType() == typeof(CharacterActor))
                ?? group.First())
            .ToArray()
            ?? Array.Empty<CharacterActor>();
    }

    public static CharacterActor GetCanonical(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        return actor.gameObject.GetComponents<CharacterActor>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.GetType() == typeof(CharacterActor))
            ?? actor;
    }
}
