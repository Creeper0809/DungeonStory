using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied level changes into the existing plain character log.
/// Progression state, stats, drafts, and notifications remain with the publisher.
/// </summary>
public sealed class CharacterLevelActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterLifetimeQuery world;
    private IDisposable subscription;

    public CharacterLevelActivityObserver(
        IGameEventBus events,
        ICharacterLifetimeQuery world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<CharacterLevelChangedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static string Project(in CharacterLevelChangedEvent value) =>
        value.Cause switch
        {
            CharacterLevelChangeCause.Experience =>
                $"레벨 {value.Level}에 도달했다.",
            CharacterLevelChangeCause.MinimumLevelRequirement => string.Empty,
            CharacterLevelChangeCause.CampaignRecruitmentTraining =>
                "원정 합류 훈련을 마쳤다.",
            CharacterLevelChangeCause.DebugAdjustment =>
                "디버그 레벨 조정",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Cause,
                "Unknown character level change cause.")
        };

    private void Observe(CharacterLevelChangedEvent value)
    {
        string message = Project(value);
        if (message.Length == 0)
            return;

        CharacterActor actor = world.AllCharacters?.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId id)
            && id.Equals(value.Character));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "character-level-activity-character-missing:"
                + value.Character.Value);
        }

        actor.AddLog(message);
    }
}
