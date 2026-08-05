using System;
using DungeonStory.Foundation;

public sealed class CharacterCombatCommandCombatServices
{
    public CharacterCombatCommandCombatServices(
        ICombatEquipmentRuntime equipment,
        ICombatResolutionService resolution,
        ICombatFiringSolutionService firingSolutions,
        ICombatLineOfSightService lineOfSight,
        ICombatCoverQuery coverQuery,
        ICombatAffiliationService affiliation,
        ICharacterBodyHealthQuery bodyHealth,
        ICombatAmmoResupplyRuntime ammoResupply)
    {
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        FiringSolutions = firingSolutions
            ?? throw new ArgumentNullException(nameof(firingSolutions));
        LineOfSight = lineOfSight
            ?? throw new ArgumentNullException(nameof(lineOfSight));
        CoverQuery = coverQuery ?? throw new ArgumentNullException(nameof(coverQuery));
        Affiliation = affiliation ?? throw new ArgumentNullException(nameof(affiliation));
        BodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        AmmoResupply = ammoResupply
            ?? throw new ArgumentNullException(nameof(ammoResupply));
    }

    public ICombatEquipmentRuntime Equipment { get; }
    public ICombatResolutionService Resolution { get; }
    public ICombatFiringSolutionService FiringSolutions { get; }
    public ICombatLineOfSightService LineOfSight { get; }
    public ICombatCoverQuery CoverQuery { get; }
    public ICombatAffiliationService Affiliation { get; }
    public ICharacterBodyHealthQuery BodyHealth { get; }
    public ICombatAmmoResupplyRuntime AmmoResupply { get; }
}

public sealed class CharacterCombatCommandWorldServices
{
    public CharacterCombatCommandWorldServices(
        IGridSystemProvider gridProvider,
        IDefenseTacticalCoordinator tacticalCoordinator,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        ICombatCoverDurabilityRegistry coverDurability,
        IGameEventBus gameEventBus,
        IWorldUiHierarchy worldUiHierarchy)
    {
        GridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        TacticalCoordinator = tacticalCoordinator
            ?? throw new ArgumentNullException(nameof(tacticalCoordinator));
        PathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        CoverDurability = coverDurability
            ?? throw new ArgumentNullException(nameof(coverDurability));
        GameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        WorldUiHierarchy = worldUiHierarchy
            ?? throw new ArgumentNullException(nameof(worldUiHierarchy));
    }

    public IGridSystemProvider GridProvider { get; }
    public IDefenseTacticalCoordinator TacticalCoordinator { get; }
    public IGridPathSearchBroker PathSearchBroker { get; }
    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public IGameClock GameClock { get; }
    public ICombatCoverDurabilityRegistry CoverDurability { get; }
    public IGameEventBus GameEventBus { get; }
    public IWorldUiHierarchy WorldUiHierarchy { get; }
}

public sealed class CharacterCombatCommandCollaborators
{
    public CharacterCombatCommandCollaborators(
        CombatAttackPositionPlanner attackPositionPlanner,
        CombatFallbackWeaponSelector fallbackWeapons,
        CombatCommandResultApplier resultApplier,
        CombatCommandParticipantQuery participants,
        ICharacterCombatUiTextQuery uiText)
    {
        AttackPositionPlanner = attackPositionPlanner
            ?? throw new ArgumentNullException(nameof(attackPositionPlanner));
        FallbackWeapons = fallbackWeapons
            ?? throw new ArgumentNullException(nameof(fallbackWeapons));
        ResultApplier = resultApplier
            ?? throw new ArgumentNullException(nameof(resultApplier));
        Participants = participants
            ?? throw new ArgumentNullException(nameof(participants));
        UiText = uiText ?? throw new ArgumentNullException(nameof(uiText));
    }

    public CombatAttackPositionPlanner AttackPositionPlanner { get; }
    public CombatFallbackWeaponSelector FallbackWeapons { get; }
    public CombatCommandResultApplier ResultApplier { get; }
    public CombatCommandParticipantQuery Participants { get; }
    public ICharacterCombatUiTextQuery UiText { get; }
}
