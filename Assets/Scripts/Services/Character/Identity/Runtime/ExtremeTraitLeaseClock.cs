using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Converts an abandoned emergency-production lease into its authored
/// aftermath. The lease is refreshed only by real production work execution,
/// so changing jobs cannot retain the speed/combat-risk benefit.
/// </summary>
public sealed class ExtremeTraitLeaseClock : ITickable
{
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly ICharacterWorldQuery world;
    private readonly IGameClock clock;

    public ExtremeTraitLeaseClock(
        ExtremeTraitRuntime extremeTraits,
        ICharacterWorldQuery world,
        IGameClock clock)
    {
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    [GameplayInternalOnly(
        "The registered ITickable entry point expires abandoned production-limit leases.",
        "ITickable|DungeonCharacterRegistration")]
    public void Tick()
    {
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor != null && !actor.IsDead)
                extremeTraits.ExpireProductionLimitBreak(actor, clock.Time);
        }
    }
}
