using System;

public sealed class CharacterCombatStanceProjection : ICharacterCombatStanceQuery
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    public CharacterCombatStanceProjection(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public bool IsInCombatStance(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        CharacterCombatCommandAggregateState state =
            aggregateRootStore.GetOrCreate(
                () => new CharacterCombatCommandAggregateState());
        return state.CombatStance.Contains(
            CharacterPersistentIdentity.Require(actor).Value);
    }
}
