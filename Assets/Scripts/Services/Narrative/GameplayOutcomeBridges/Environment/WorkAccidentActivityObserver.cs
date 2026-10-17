using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class WorkAccidentActivityObserver : IStartable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable subscription;
    public WorkAccidentActivityObserver(IGameEventBus events, ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }
    public void Start() => subscription ??= events.Subscribe<CharacterInjuredIdentityEvent>(Observe);
    public void Dispose() { subscription?.Dispose(); subscription = null; }
    private void Observe(CharacterInjuredIdentityEvent fact)
    {
        if (!fact.HasWorkAccidentEvidence) return;
        var actor = world.Characters.FirstOrDefault(value => value != null
            && CharacterPersistentIdentity.TryGet(value, out var id) && id.Equals(fact.Character));
        var facility = world.Buildings.FirstOrDefault(value => value != null
            && value.PersistentInstanceId.Equals(fact.WorkAccidentFacilityInstanceId));
        actor?.AddActivity(CharacterActivityEvent.Work(fact.WorkAccidentWorkTypeId,
            CharacterActivityOutcomes.Failed, $"작업 사고로 작업이 중단됨 ({fact.WorkAccidentObservedCause})",
            facility, reasonCode: "work-accident", value: fact.WorkAccidentRiskMultiplier, bubbleEligible: true));
    }
}
