using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>Existing invasion events are observed independently of skill activation.</summary>
public sealed class InvasionExposureExperienceObserver : IStartable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterWorldQuery characters;
    private IDisposable subscription;

    public InvasionExposureExperienceObserver(IGameEventBus events, ICharacterWorldQuery characters)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
    }

    public void Start() => subscription ??= events.Subscribe<InvasionSpawnedEvent>(Observe);
    public void Dispose() { subscription?.Dispose(); subscription = null; }

    private void Observe(InvasionSpawnedEvent fact)
    {
        var living = CharacterActorCollection.DistinctByGameObject(characters.Characters)
            .Where(actor => actor != null && !actor.IsDead).ToArray();
        foreach (var actor in living)
            actor.Progression?.RecordNarrative(CharacterNarrativeDomain.Invasion,
                "invasion-started", fact.threatSnapshot.stage.ToString(), "faced", fact.threatSnapshot.threat);
    }
}
