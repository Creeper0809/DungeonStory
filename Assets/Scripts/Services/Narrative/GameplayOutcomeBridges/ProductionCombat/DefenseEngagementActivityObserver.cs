using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects defense engagement lifecycle notifications into the existing
/// character activity presentation. Combat and persistence remain owner-side.
/// </summary>
public sealed class DefenseEngagementActivityObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable subscription;

    public DefenseEngagementActivityObserver(
        IGameEventBus events,
        ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<DefenseEngagementChangedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static CharacterActivityEvent Project(
        in DefenseEngagementChangedEvent value)
    {
        value.Validate();
        return value.Transition switch
        {
            DefenseEngagementTransition.Dispatched =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Combat,
                    CharacterActivityOutcomes.Started,
                    value.DispatchDetail,
                    actionId: "defense:dispatch",
                    sentiment: -0.05f),
            DefenseEngagementTransition.Started =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Combat,
                    CharacterActivityOutcomes.Started,
                    $"Defend against {value.IntruderDisplayLabel}",
                    actionId: "defense:engagement",
                    targetId: value.TargetId,
                    targetName: value.TargetName,
                    sentiment: -0.1f,
                    bubbleEligible: true),
            DefenseEngagementTransition.Completed =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Combat,
                    CharacterActivityOutcomes.Completed,
                    "Intruder defense completed",
                    actionId: "defense:engagement",
                    targetName: value.TargetName,
                    value: value.ExchangeCount,
                    sentiment: 0.7f,
                    bubbleEligible: true),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Transition,
                "Unknown defense engagement transition.")
        };
    }

    private void Observe(DefenseEngagementChangedEvent value)
    {
        value.Validate();
        CharacterActor actor = world.AllCharacters.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId id)
            && id.Equals(value.Subject));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "defense-engagement-activity-character-missing:"
                + value.Subject.Value);
        }

        actor.AddActivity(Project(value));
    }
}
