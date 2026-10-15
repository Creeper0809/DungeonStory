using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>Presentation follows applied incidents; species owners never construct activity logs.</summary>
public sealed class SpeciesIncidentActivityObserver : IStartable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable subscription;

    public SpeciesIncidentActivityObserver(IGameEventBus events, ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Start() => subscription ??= events.Subscribe<SpeciesIncidentTriggeredEvent>(Observe);
    public void Dispose() { subscription?.Dispose(); subscription = null; }

    private void Observe(SpeciesIncidentTriggeredEvent fact)
    {
        var actor = world.Characters.FirstOrDefault(value => value != null
            && CharacterPersistentIdentity.TryGet(value, out var id) && id.Equals(fact.CharacterId));
        actor?.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Social, CharacterActivityOutcomes.Failed, fact.Summary,
            null, actionId: fact.IncidentId, reasonCode: "species-discontent", bubbleEligible: true));
    }
}
