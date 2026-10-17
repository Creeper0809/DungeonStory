using System.Collections.Generic;
using DungeonStory.Foundation;

/// <summary>Shared live consumer eligibility used by nutrition and treatment.</summary>
internal static class SurvivalConsumerSelection
{
    internal static IEnumerable<CharacterActor> GetSurvivalConsumers(ICharacterAiWorldRegistry worldRegistry)
    {
        IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor != null
                && !actor.IsDead
                && IsSurvivalConsumer(actor))
            {
                yield return actor;
            }
        }
    }

    internal static int CountSurvivalConsumers(ICharacterAiWorldRegistry worldRegistry)
    {
        int count = 0;
        IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
        for (int index = 0; index < actors.Count; index++)
        {
            CharacterActor actor = actors[index];
            if (actor != null
                && !actor.IsDead
                && IsSurvivalConsumer(actor))
            {
                count++;
            }
        }

        return count;
    }

    internal static bool IsSurvivalConsumer(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && (actor.Role == CharacterRole.Owner
                || CharacterWorkRoleUtility.TryGetWork(actor, out _));
    }
}
