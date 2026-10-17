using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects an applied departure into the existing character activity feed.
/// Visitor lifecycle and publication state remain owned by the character domain.
/// </summary>
public sealed class VisitorDepartureActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterLifetimeQuery world;
    private IDisposable subscription;

    public VisitorDepartureActivityObserver(
        IGameEventBus events,
        ICharacterLifetimeQuery world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<VisitorDepartureAppliedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static CharacterActivityEvent Project(
        in VisitorDepartureAppliedEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Completed,
            value.DisplayName + " left the dungeon.",
            actionId: "visitor:exit-dungeon",
            targetId: value.CharacterId,
            targetName: value.DisplayName,
            reasonCode: "visitor-departed",
            bubbleEligible: false);

    private void Observe(VisitorDepartureAppliedEvent value)
    {
        CharacterActor actor = world.AllCharacters?.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && string.Equals(
                id.Value,
                value.CharacterId,
                StringComparison.Ordinal));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "visitor-departure-activity-character-missing:"
                + value.CharacterId);
        }

        actor.AddActivity(Project(value), value.AbsoluteDay);
    }
}
