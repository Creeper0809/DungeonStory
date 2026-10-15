using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects newly applied memory erasure into the existing plain character log.
/// The terminal fact and acquired-trait state remain owned by gameplay.
/// </summary>
public sealed class MemoryErasureActivityObserver : IInitializable, IDisposable
{
    private const string SucceededMessage =
        "기억 소거 인장을 사용해 선택한 획득 특성을 지웠습니다.";

    private readonly IGameEventBus events;
    private readonly ICharacterLifetimeQuery world;
    private IDisposable subscription;

    public MemoryErasureActivityObserver(
        IGameEventBus events,
        ICharacterLifetimeQuery world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<MemoryErasureSealUseCompletedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(MemoryErasureSealUseCompletedEvent value)
    {
        if (value.Result.Status != MemoryErasureSealUseStatus.Succeeded)
            return;

        CharacterActor actor = world.AllCharacters?.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && string.Equals(
                id.Value,
                value.Result.TargetCharacterId,
                StringComparison.Ordinal));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "memory-erasure-activity-character-missing:"
                + value.Result.TargetCharacterId);
        }

        actor.AddLog(SucceededMessage);
    }
}
