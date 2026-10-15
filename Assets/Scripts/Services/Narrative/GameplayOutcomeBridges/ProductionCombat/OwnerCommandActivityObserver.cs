using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects rejected direct owner commands into the existing activity log.
/// Command validation, notices, and state remain with the publisher.
/// </summary>
public sealed class OwnerCommandActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable subscription;

    public OwnerCommandActivityObserver(
        IGameEventBus events,
        ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<OwnerCommandRejectedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static CharacterActivityEvent Project(
        in OwnerCommandRejectedEvent value) => value.Kind switch
    {
        OwnerCommandRejectedKind.PriorityWork =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Command,
                CharacterActivityOutcomes.Failed,
                $"우선 지정 실패: {value.Detail}",
                actionId: "command:priority-work",
                targetId: value.TargetId,
                targetName: value.TargetName,
                reasonCode: value.ReasonCode,
                facilityId: value.TargetDefinitionId,
                sentiment: -0.7f,
                narrativeEligible: true,
                bubbleEligible: true),
        OwnerCommandRejectedKind.Suppress =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Command,
                CharacterActivityOutcomes.Failed,
                $"우선 지정 실패: {value.Detail}",
                actionId: "command:priority-suppress",
                targetId: value.TargetId,
                targetName: value.TargetName,
                reasonCode: value.ReasonCode,
                sentiment: -0.7f,
                narrativeEligible: true,
                bubbleEligible: true),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value.Kind,
            "Unknown owner-command rejection kind.")
    };

    private void Observe(OwnerCommandRejectedEvent value)
    {
        CharacterActor actor = world.AllCharacters.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId id)
            && id.Equals(value.Character));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "owner-command-activity-character-missing:"
                + value.Character.Value);
        }

        actor.AddActivity(Project(value));
    }
}
