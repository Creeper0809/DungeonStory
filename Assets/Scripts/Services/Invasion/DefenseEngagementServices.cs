using System;
using DungeonStory.Foundation;

public sealed class DefenseEngagementWorldServices
{
    public DefenseEngagementWorldServices(
        IStaffWorkforceQueryService workforce,
        IGridSystemProvider grid,
        IInvasionIntruderContext invasion,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IInvasionOwnerEvacuationService ownerEvacuation,
        ICharacterWorldQuery characters,
        IGameEventBus events,
        IGameClock clock)
    {
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Invasion = invasion ?? throw new ArgumentNullException(nameof(invasion));
        Director = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .Director
            ?? throw new InvalidOperationException(
                $"{nameof(DefenseEngagementWorldServices)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        OwnerEvacuation = ownerEvacuation ?? throw new ArgumentNullException(nameof(ownerEvacuation));
        Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IStaffWorkforceQueryService Workforce { get; }
    public IGridSystemProvider Grid { get; }
    public IInvasionIntruderContext Invasion { get; }
    public InvasionDirectorRuntime Director { get; }
    public IInvasionOwnerEvacuationService OwnerEvacuation { get; }
    public ICharacterWorldQuery Characters { get; }
    public IGameEventBus Events { get; }
    public IGameClock Clock { get; }
}

public sealed class DefenseEngagementCombatServices
{
    public DefenseEngagementCombatServices(
        IDefenseResponsePolicyRuntime policy,
        ICombatLineOfSightService lineOfSight,
        ICombatCoverQuery cover,
        IDefenseCombatExecutor executor,
        ICombatAmmoResupplyRuntime ammoResupply,
        IDefenseTacticalCoordinator tactics,
        IDefenseEngagementStore store,
        IGridPathSearchBroker pathSearch,
        ICharacterPerformanceQuery performance)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        LineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
        Cover = cover ?? throw new ArgumentNullException(nameof(cover));
        Executor = executor ?? throw new ArgumentNullException(nameof(executor));
        AmmoResupply = ammoResupply ?? throw new ArgumentNullException(nameof(ammoResupply));
        Tactics = tactics ?? throw new ArgumentNullException(nameof(tactics));
        Store = store ?? throw new ArgumentNullException(nameof(store));
        PathSearch = pathSearch ?? throw new ArgumentNullException(nameof(pathSearch));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
    }

    public IDefenseResponsePolicyRuntime Policy { get; }
    public ICombatLineOfSightService LineOfSight { get; }
    public ICombatCoverQuery Cover { get; }
    public IDefenseCombatExecutor Executor { get; }
    public ICombatAmmoResupplyRuntime AmmoResupply { get; }
    public IDefenseTacticalCoordinator Tactics { get; }
    public IDefenseEngagementStore Store { get; }
    public IGridPathSearchBroker PathSearch { get; }
    public ICharacterPerformanceQuery Performance { get; }
}
